#!/bin/bash

# JobSharp Test Runner with Code Coverage
# This script runs all tests and generates code coverage reports

set -e

CONFIGURATION="Debug"
OUTPUT="TestResults"
SKIP_BUILD=false
OPEN_REPORT=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -o|--output)
            OUTPUT="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --open-report)
            OPEN_REPORT=true
            shift
            ;;
        *)
            echo "Unknown option $1"
            exit 1
            ;;
    esac
done

echo -e "\033[32mJobSharp Test Runner\033[0m"
echo -e "\033[32m===================\033[0m"

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
OUTPUT_PATH="$SCRIPT_DIR/$OUTPUT"

# Create output directory
rm -rf "$OUTPUT_PATH"
mkdir -p "$OUTPUT_PATH"

# Build solution if not skipped
if [ "$SKIP_BUILD" = false ]; then
    echo -e "\033[33mBuilding solution...\033[0m"
    dotnet build "$SCRIPT_DIR/../JobSharp.sln" -c "$CONFIGURATION" --no-restore
fi

# Test projects
TEST_PROJECTS=(
    "JobSharp.Tests"
    "JobSharp.EntityFramework.Tests"
    "JobSharp.Dapper.Tests"
    "JobSharp.MongoDb.Tests"
    "JobSharp.Redis.Tests"
    "JobSharp.Cassandra.Tests"
)

ALL_RESULTS=()
PASS_COUNT=0
FAIL_COUNT=0

echo -e "\033[33mRunning tests with coverage...\033[0m"

for PROJECT in "${TEST_PROJECTS[@]}"; do
    PROJECT_PATH="$SCRIPT_DIR/$PROJECT/$PROJECT.csproj"
    if [ -f "$PROJECT_PATH" ]; then
        echo -e "\033[36mTesting $PROJECT...\033[0m"
        
        COVERAGE_FILE="$OUTPUT_PATH/$PROJECT.coverage.xml"
        
        if dotnet test "$PROJECT_PATH" \
            -c "$CONFIGURATION" \
            --no-build \
            --logger "trx;LogFileName=$PROJECT.trx" \
            --results-directory "$OUTPUT_PATH" \
            --collect:"XPlat Code Coverage" \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:CoverletOutput="$COVERAGE_FILE" \
            /p:Threshold=80 \
            /p:ThresholdType=line \
            /p:ThresholdStat=total; then
            
            echo -e "\033[32m✓ $PROJECT passed\033[0m"
            ALL_RESULTS+=("$PROJECT:PASS")
            ((PASS_COUNT++))
        else
            echo -e "\033[31m✗ $PROJECT failed\033[0m"
            ALL_RESULTS+=("$PROJECT:FAIL")
            ((FAIL_COUNT++))
        fi
    else
        echo -e "\033[33mWarning: Project not found: $PROJECT_PATH\033[0m"
    fi
done

# Generate combined coverage report
echo -e "\033[33mGenerating coverage report...\033[0m"

COVERAGE_FILES=$(find "$OUTPUT_PATH" -name "*.cobertura.xml" -type f)
if [ -n "$COVERAGE_FILES" ]; then
    # Install ReportGenerator if not available
    if ! command -v reportgenerator &> /dev/null; then
        echo -e "\033[33mInstalling ReportGenerator...\033[0m"
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi
    
    COVERAGE_PATTERN=$(echo "$COVERAGE_FILES" | tr '\n' ';' | sed 's/;$//')
    REPORT_DIR="$OUTPUT_PATH/coverage-report"
    
    reportgenerator \
        -reports:"$COVERAGE_PATTERN" \
        -targetdir:"$REPORT_DIR" \
        -reporttypes:"Html;HtmlSummary;Badges;TextSummary" \
        -historydir:"$REPORT_DIR/history"
    
    echo -e "\033[32mCoverage report generated: $REPORT_DIR/index.html\033[0m"
    
    if [ "$OPEN_REPORT" = true ]; then
        if command -v xdg-open &> /dev/null; then
            xdg-open "$REPORT_DIR/index.html"
        elif command -v open &> /dev/null; then
            open "$REPORT_DIR/index.html"
        fi
    fi
fi

# Summary
echo -e "\n\033[32mTest Results Summary:\033[0m"
echo "===================="

TOTAL_COUNT=${#ALL_RESULTS[@]}

for RESULT in "${ALL_RESULTS[@]}"; do
    PROJECT=$(echo "$RESULT" | cut -d':' -f1)
    STATUS=$(echo "$RESULT" | cut -d':' -f2)
    
    if [ "$STATUS" = "PASS" ]; then
        echo -e "\033[32m✓ $PROJECT: $STATUS\033[0m"
    else
        echo -e "\033[31m✗ $PROJECT: $STATUS\033[0m"
    fi
done

if [ $FAIL_COUNT -eq 0 ]; then
    echo -e "\n\033[32mTotal: $TOTAL_COUNT, Passed: $PASS_COUNT, Failed: $FAIL_COUNT\033[0m"
    echo -e "\033[32mAll tests passed! 🎉\033[0m"
    exit 0
else
    echo -e "\n\033[31mTotal: $TOTAL_COUNT, Passed: $PASS_COUNT, Failed: $FAIL_COUNT\033[0m"
    echo -e "\033[31mSome tests failed! 😞\033[0m"
    exit 1
fi 