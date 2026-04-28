$ErrorActionPreference = 'Stop'

$proj    = 'd:\FYP\Military-Training-System'
$asmDir  = Join-Path $proj 'Library\ScriptAssemblies'
$pkgDir  = Join-Path $proj 'Library\PackageCache\com.unity.nuget.newtonsoft-json@4dfd81071c64\Runtime\AOT'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.3.3f1\Editor\Data\Managed\UnityEngine'
$facades = 'C:\Program Files\Unity\Hub\Editor\6000.3.3f1\Editor\Data\MonoBleedingEdge\lib\mono\net_4_x-win32\Facades'

# Resolve dependent assemblies on demand (Unity engine modules)
$resolveHandler = {
    param($sender, $args)
    $name = ($args.Name -split ',')[0]
    foreach ($dir in @($facades, $unity, $asmDir, $pkgDir)) {
        $candidate = Join-Path $dir ($name + '.dll')
        if (Test-Path $candidate) {
            return [System.Reflection.Assembly]::LoadFrom($candidate)
        }
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)

# Load core dependencies up front
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $facades 'netstandard.dll'))
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $unity 'UnityEngine.CoreModule.dll'))
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $unity 'UnityEngine.dll'))
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $pkgDir 'Newtonsoft.Json.dll'))
$asm = [System.Reflection.Assembly]::LoadFrom((Join-Path $asmDir 'TeamSentinels.ScenarioGeneration.dll'))

Write-Host '=== Loaded TeamSentinels.ScenarioGeneration.dll ==='

# Replace Unity's default log handler so Debug.Log calls don't crash on the
# missing native Unity runtime. We just print log messages to the host.
$stubSrc = @'
using System;
using UnityEngine;

public class StubLogHandler : ILogHandler {
    public void LogException(Exception exception, UnityEngine.Object context) {
        Console.Error.WriteLine("[unity-log-exception] " + exception);
    }
    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) {
        // Silenced so the JSON output stays clean. Uncomment to debug.
        // Console.WriteLine("[unity-log] " + string.Format(format, args));
    }
}
'@
$unityCore = Join-Path $unity 'UnityEngine.CoreModule.dll'
$nsRef     = Join-Path $facades 'netstandard.dll'
Add-Type -TypeDefinition $stubSrc -ReferencedAssemblies @($unityCore, $nsRef) -Language CSharp
[UnityEngine.Debug]::unityLogger.logHandler = New-Object StubLogHandler

# Build ScenarioConfig with defaults (matches ScenarioConfig.cs default values)
$cfgType = $asm.GetType('TeamSentinels.ScenarioGeneration.DataModels.ScenarioConfig', $true)
$config = [System.Activator]::CreateInstance($cfgType)
Write-Host ('config null? ' + ($null -eq $config))
Write-Host ('config type: ' + $config.GetType().FullName)
Write-Host ('missionStructure null? ' + ($null -eq $config.missionStructure))
Write-Host ('entityConfiguration null? ' + ($null -eq $config.entityConfiguration))
Write-Host ('executionControls null? ' + ($null -eq $config.executionControls))

# Fix the seed so the dry-run is deterministic
$controls = $config.executionControls
$seedField = $controls.GetType().GetField('seed')
$seedField.SetValue($controls, [System.Nullable[int]]20260419)

Write-Host ("=== Config: layout={0}, rooms=[{1}-{2}], terrorists={3}, difficulty={4}, seed={5} ===" -f `
    $config.missionStructure.layoutType, $config.missionStructure.roomCount.min, $config.missionStructure.roomCount.max, `
    $config.entityConfiguration.terroristCount, $controls.difficultyLevel, $controls.seed)

# Construct ScenarioGenerator and run Generate(config)
$generator = $asm.CreateInstance('TeamSentinels.ScenarioGeneration.Generators.ScenarioGenerator')
$generateMethod = $generator.GetType().GetMethod('Generate', [type[]]@($config.GetType()))

$scenario = $null
try {
    $scenario = $generateMethod.Invoke($generator, @($config))
} catch {
    Write-Host '=== Generator threw ==='
    Write-Host $_.Exception.ToString()
    if ($_.Exception.InnerException) {
        Write-Host '--- InnerException ---'
        Write-Host $_.Exception.InnerException.ToString()
    }
    exit 1
}

Write-Host '=== Generation complete ==='

# Serialise the scenario
$exporterType = $asm.GetType('TeamSentinels.ScenarioGeneration.IO.ScenarioExporter')
$serialiseMethod = $exporterType.GetMethod('SerialiseToJson')
$json = $serialiseMethod.Invoke($null, @($scenario, $false))

Write-Host ('=== JSON length: {0} chars ===' -f $json.Length)

$lines = $json -split "`r?`n"
Write-Host ('=== Total JSON lines: {0} ===' -f $lines.Length)
Write-Host '=== First 200 lines ==='
$take = [Math]::Min(200, $lines.Length)
for ($i = 0; $i -lt $take; $i++) {
    Write-Host $lines[$i]
}
