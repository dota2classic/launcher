param(
    [string]$SpecUrl = "https://api.dotaclassic.ru/api-json",
    [string]$RawSpecPath = "api-openapi.json",
    [string]$SanitizedSpecPath = "api-openapi.sanitized.json",
    [string]$OutputPath = "Generated/DotaclassicApiClient.g.cs"
)

$ErrorActionPreference = "Stop"

Write-Host "Downloading OpenAPI spec..."
Invoke-WebRequest -Uri $SpecUrl -UseBasicParsing | Select-Object -ExpandProperty Content | Set-Content -Path $RawSpecPath

Write-Host "Sanitizing spec..."
$raw = Get-Content $RawSpecPath -Raw
$doc = $raw | ConvertFrom-Json

function Fix-Node {
    param(
        [object]$Node,
        [bool]$IsSchemaProperty
    )

    if ($null -eq $Node) { return }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        if ($IsSchemaProperty -and ($Node.PSObject.Properties.Name -contains "required") -and ($Node.required -is [bool])) {
            $Node.PSObject.Properties.Remove("required")
        }

        if ($Node.PSObject.Properties.Name -contains "properties") {
            foreach ($prop in $Node.properties.PSObject.Properties) {
                Fix-Node -Node $prop.Value -IsSchemaProperty $true
            }
        }

        foreach ($prop in $Node.PSObject.Properties) {
            if ($prop.Name -ne "properties") {
                Fix-Node -Node $prop.Value -IsSchemaProperty $false
            }
        }
        return
    }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [string])) {
        foreach ($item in $Node) {
            Fix-Node -Node $item -IsSchemaProperty $false
        }
    }
}

Fix-Node -Node $doc -IsSchemaProperty $false
$doc | ConvertTo-Json -Depth 100 -Compress | Set-Content -Path $SanitizedSpecPath

Write-Host "Generating C# client..."
$nswag = Join-Path $env:USERPROFILE ".nuget\packages\nswag.msbuild\14.6.2\tools\Net80\dotnet-nswag.dll"
if (-not (Test-Path $nswag)) {
    throw "NSwag tool not found at '$nswag'."
}

$outputDir = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

dotnet --roll-forward-on-no-candidate-fx 2 $nswag openapi2csclient `
    /className:DotaclassicApiClient `
    /namespace:d2c_launcher.Api `
    /input:$SanitizedSpecPath `
    /output:$OutputPath `
    /GenerateClientInterfaces:true `
    /GenerateOptionalParameters:true `
    /InjectHttpClient:true `
    /UseBaseUrl:false `
    /GenerateBaseUrlProperty:false `
    /JsonLibrary:SystemTextJson `
    /GenerateExceptionClasses:true `
    /OperationGenerationMode:SingleClientFromOperationId

Write-Host "OpenAPI client refreshed at $OutputPath"
