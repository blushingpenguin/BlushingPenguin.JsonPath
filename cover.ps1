#!/usr/bin/pwsh

Param(
    [switch]
    [Parameter(
        Mandatory = $false,
        HelpMessage = "Enable generation of a cobertura report")]
    $cobertura
)

$ErrorActionPreference="Stop"
Set-StrictMode -Version Latest

function script:exec {
    [CmdletBinding()]

	param(
		[Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
		[Parameter(Position=1,Mandatory=0)][string]$errorMessage = ("Error executing command: {0}" -f $cmd)
    )
    # write-host $cmd
	& $cmd
	if ($lastexitcode -ne 0)
	{
		throw $errorMessage
	}
}

function dotnetTest([string[]] $asmAndIgnores)
{
    $filter = '\"[*TestAdapter*]*\"'
    for ($i = 1; $i -lt $asmAndIgnores.Length; ++$i)
    {
        $filter += ",[" + $asmAndIgnores[$i] + "]*";
    }
    $filter += '\"';
    $asm = $asmAndIgnores[0];

    exec { dotnet test `
        --configuration Release `
        /p:CollectCoverage=true `
        /p:Exclude=$filter `
        /p:CoverletOutputFormat=cobertura `
        /p:CoverletOutput="../coverage/${asm}.coverage.xml" `
        /p:CopyLocalLockFileAssemblies=true `
        /p:Threshold=0 `
        /p:ThresholdType=line `
        "${asm}/${asm}.csproj" }
}

# Install ReportGenerator
if (!(Test-Path "tools/reportgenerator") -and !(Test-Path "tools/reportgenerator.exe"))
{
    #Using alternate nuget.config due to https://github.com/dotnet/cli/issues/9586
    exec { dotnet tool install --configfile nuget.tool.config --tool-path tools dotnet-reportgenerator-globaltool }
}

Write-Host "Running dotnet test"
$asms = @( `
    , @("BlushingPenguin.JsonPath.Test")
)
foreach ($asm in $asms) {
    dotnetTest $asm
}

Write-Host "Running ReportGenerator"
$reportTypes="-reporttypes:Html"
if ($cobertura)
{
    $reportTypes += ";Cobertura"
}
$reports = ""
foreach ($asm in $asms) {
    if ($reports) {
        $reports += ";"
    }
    $reports += "coverage/" + $asm[0] + ".coverage.xml"
}
$reports = "-reports:" + $reports
write-host $reports
exec { tools/reportgenerator $reports "-targetdir:coverage" $reportTypes }
