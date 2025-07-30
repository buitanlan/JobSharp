# JobSharp Test Runner with Code Coverage
# This script runs all tests and generates code coverage reports

param(
    [string]$Configuration = "Debug",
    [string]$Output = "TestResults",
    [switch]$SkipBuild,
    [switch]$OpenReport
)

Write-Host "JobSharp Test Runner" -ForegroundColor Green
Write-Host "===================" -ForegroundColor Green

# Create output directory
$OutputPath = Join-Path $PSScriptRoot $Output
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath | Out-Null

# Build solution if not skipped
if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build ../JobSharp.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
}

# Test projects
$TestProjects = @(
    "JobSharp.Tests",
    "JobSharp.EntityFramework.Tests", 
    "JobSharp.Dapper.Tests",
    "JobSharp.MongoDb.Tests",
    "JobSharp.Redis.Tests",
    "JobSharp.Cassandra.Tests"
)

$AllResults = @()

Write-Host "Running tests with coverage..." -ForegroundColor Yellow

foreach ($Project in $TestProjects) {
    $ProjectPath = Join-Path $PSScriptRoot "$Project/$Project.csproj"
    if (Test-Path $ProjectPath) {
        Write-Host "Testing $Project..." -ForegroundColor Cyan
        
        $CoverageFile = Join-Path $OutputPath "$Project.coverage.xml"
        
        dotnet test $ProjectPath `
            -c $Configuration `
            --no-build `
            --logger "trx;LogFileName=$Project.trx" `
            --results-directory $OutputPath `
            --collect:"XPlat Code Coverage" `
            --settings ../coverlet.runsettings `
            /p:CollectCoverage=true `
            /p:CoverletOutputFormat=cobertura `
            /p:CoverletOutput=$CoverageFile `
            /p:Threshold=80 `
            /p:ThresholdType=line `
            /p:ThresholdStat=total

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ $Project passed" -ForegroundColor Green
            $AllResults += @{ Project = $Project; Result = "PASS" }
        } else {
            Write-Host "✗ $Project failed" -ForegroundColor Red
            $AllResults += @{ Project = $Project; Result = "FAIL" }
        }
    } else {
        Write-Warning "Project not found: $ProjectPath"
    }
}

# Generate combined coverage report
Write-Host "Generating coverage report..." -ForegroundColor Yellow

$CoverageFiles = Get-ChildItem -Path $OutputPath -Filter "*.cobertura.xml" -Recurse
if ($CoverageFiles.Count -gt 0) {
    # Install ReportGenerator if not available
    if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
        Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
        dotnet tool install -g dotnet-reportgenerator-globaltool
    }
    
    $CoveragePattern = ($CoverageFiles.FullName -join ";")
    $ReportDir = Join-Path $OutputPath "coverage-report"
    
    reportgenerator `
        -reports:$CoveragePattern `
        -targetdir:$ReportDir `
        -reporttypes:"Html;HtmlSummary;Badges;TextSummary" `
        -historydir:$ReportDir\history
    
    Write-Host "Coverage report generated: $ReportDir\index.html" -ForegroundColor Green
    
    if ($OpenReport) {
        Start-Process (Join-Path $ReportDir "index.html")
    }
}

# Summary
Write-Host "`nTest Results Summary:" -ForegroundColor Green
Write-Host "===================="

$PassCount = ($AllResults | Where-Object { $_.Result -eq "PASS" }).Count
$FailCount = ($AllResults | Where-Object { $_.Result -eq "FAIL" }).Count

foreach ($Result in $AllResults) {
    $Color = if ($Result.Result -eq "PASS") { "Green" } else { "Red" }
    $Symbol = if ($Result.Result -eq "PASS") { "✓" } else { "✗" }
    Write-Host "$Symbol $($Result.Project): $($Result.Result)" -ForegroundColor $Color
}

Write-Host "`nTotal: $($AllResults.Count), Passed: $PassCount, Failed: $FailCount" -ForegroundColor $(if ($FailCount -eq 0) { "Green" } else { "Red" })

if ($FailCount -eq 0) {
    Write-Host "All tests passed! 🎉" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some tests failed! 😞" -ForegroundColor Red
    exit 1
} 