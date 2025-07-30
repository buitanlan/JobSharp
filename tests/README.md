# JobSharp Test Suite

This directory contains comprehensive unit tests for the JobSharp library and all its storage providers. The test suite is designed to achieve **80% code coverage** across all projects.

## Test Structure

```
tests/
├── JobSharp.Tests/                    # Core library tests
│   ├── Core/                         # Core component tests
│   ├── Processing/                   # Job processing tests
│   ├── Scheduling/                   # Cron scheduling tests  
│   └── Storage/                      # Storage interface tests
├── JobSharp.EntityFramework.Tests/   # Entity Framework provider tests
├── JobSharp.Dapper.Tests/           # Dapper provider tests
├── JobSharp.MongoDb.Tests/          # MongoDB provider tests
├── JobSharp.Redis.Tests/            # Redis provider tests
├── run-tests.ps1                    # PowerShell test runner (Windows)
├── run-tests.sh                     # Bash test runner (Linux/Mac)
└── README.md                        # This file
```

## Testing Technologies

- **xUnit**: Primary testing framework
- **Shouldly**: Fluent assertion library for readable tests
- **NSubstitute**: Mocking framework for dependencies
- **Coverlet**: Code coverage analysis
- **ReportGenerator**: HTML coverage report generation

## Running Tests

### Prerequisites

1. .NET 8.0 SDK
2. All project dependencies restored (`dotnet restore`)

### Quick Start

**Windows (PowerShell):**
```powershell
./run-tests.ps1
```

**Linux/Mac (Bash):**
```bash
chmod +x run-tests.sh
./run-tests.sh
```

### Advanced Usage

**PowerShell:**
```powershell
# Run tests with specific configuration
./run-tests.ps1 -Configuration Release

# Skip build step
./run-tests.ps1 -SkipBuild

# Open coverage report automatically
./run-tests.ps1 -OpenReport

# Custom output directory
./run-tests.ps1 -Output "MyTestResults"
```

**Bash:**
```bash
# Run tests with specific configuration
./run-tests.sh --configuration Release

# Skip build step
./run-tests.sh --skip-build

# Open coverage report (Linux/Mac)
./run-tests.sh --open-report

# Custom output directory
./run-tests.sh --output "MyTestResults"
```

### Individual Test Projects

You can run individual test projects using standard dotnet commands:

```bash
# Run core library tests
dotnet test JobSharp.Tests/

# Run Entity Framework tests
dotnet test JobSharp.EntityFramework.Tests/

# Run with coverage
dotnet test JobSharp.Tests/ --collect:"XPlat Code Coverage"
```

## Coverage Requirements

The test suite targets **80% line coverage** across all projects:

- **JobSharp** (Core Library): 80%+ coverage
- **JobSharp.EntityFramework**: 80%+ coverage  
- **JobSharp.Dapper**: 80%+ coverage
- **JobSharp.MongoDb**: 80%+ coverage
- **JobSharp.Redis**: 80%+ coverage
- **JobSharp.Cassandra**: 80%+ coverage

Coverage is measured using Coverlet and reports are generated with ReportGenerator.

## Test Categories

### Unit Tests
- **Job Models**: Test job state management, property validation
- **Job Handlers**: Test job execution, error handling, cancellation
- **Job Client**: Test job enqueueing, scheduling, batch operations
- **Job Processor**: Test job processing pipeline, concurrency
- **Cron Expressions**: Test cron parsing and next occurrence calculation

### Integration Tests
- **Storage Providers**: Test CRUD operations, querying, transactions
- **Database Specific**: Test provider-specific features and optimizations
- **End-to-End**: Test complete job lifecycle from creation to completion

### Mock-Based Tests
- **Dependency Injection**: Test service registration and resolution
- **Logging**: Test log message generation and levels
- **Error Scenarios**: Test exception handling and error states

## Output Files

After running tests, you'll find:

```
TestResults/
├── *.trx                            # Test result files
├── *.cobertura.xml                  # Coverage data files
└── coverage-report/                 # HTML coverage report
    ├── index.html                   # Main coverage report
    ├── summary.html                 # Coverage summary
    └── history/                     # Coverage trend data
```

## Continuous Integration

The test scripts are designed to work in CI/CD environments:

- Exit code 0: All tests passed
- Exit code 1: One or more tests failed
- Coverage threshold enforcement (fails build if below 80%)
- XML output for CI integration
- Parallel test execution support

## Troubleshooting

### Common Issues

1. **Build Failures**: Ensure all dependencies are restored
   ```bash
   dotnet restore ../JobSharp.sln
   ```

2. **Missing ReportGenerator**: The script auto-installs it
   ```bash
   dotnet tool install -g dotnet-reportgenerator-globaltool
   ```

3. **Permission Issues (Linux/Mac)**: Make script executable
   ```bash
   chmod +x run-tests.sh
   ```

4. **Port Conflicts**: Some tests may require available ports for in-memory databases

### Database-Specific Tests

- **Entity Framework**: Uses in-memory database
- **Dapper**: Uses SQLite in-memory database  
- **MongoDB**: Uses Mongo2Go (embedded MongoDB)
- **Redis**: Requires Redis server or uses mocked connections
- **Cassandra**: Uses embedded or containerized Cassandra

## Contributing

When adding new tests:

1. Follow the existing naming conventions
2. Use Shouldly for assertions
3. Mock external dependencies with NSubstitute
4. Aim for comprehensive coverage of happy path and error scenarios
5. Include both positive and negative test cases
6. Test boundary conditions and edge cases

## Performance

The test suite is optimized for:

- **Parallel Execution**: Tests run in parallel where possible
- **Fast Feedback**: Core tests complete quickly
- **Isolated Tests**: No shared state between tests
- **Resource Cleanup**: Proper disposal of test resources 