param([Parameter(Mandatory=$true)][string]$Tag,[string]$Builder='')
$ErrorActionPreference = 'Stop'
if ($Tag -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$' -or $Tag -eq 'REPLACE_WITH_COMMIT') { throw 'Use a specific immutable release tag.' }
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$contexts = [ordered]@{frontend='frontend/angular-app';gateway='gateway';backend='backend';'auth-service'='services/auth-service';'community-service'='services/community-service';'sensor-service'='services/sensor-service';'alert-service'='services/alert-service';'report-service'='services/report-service';'audit-service'='services/audit-service'}
foreach ($name in $contexts.Keys) {
  $buildArgs = if ($Builder) { @('buildx','build','--builder',$Builder,'--load') } else { @('build') }
  $buildArgs += @('--label',"org.opencontainers.image.revision=$Tag",'-t',"vigiarural/${name}:$Tag")
  if ($name -eq 'report-service') { $buildArgs += @('--target','runtime') }
  $buildArgs += (Join-Path $repoRoot $contexts[$name])
  # Windows PowerShell treats native stderr progress as ErrorRecords; use the exit code.
  $previousPreference = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  & docker @buildArgs 2>&1 | ForEach-Object { $_.ToString() }
  $buildExitCode = $LASTEXITCODE
  $ErrorActionPreference = $previousPreference
  if ($buildExitCode -ne 0) { throw "Build failed: $name" }
}
# No push. Export/import into EVERY eligible K3s node or use an approved private registry.
