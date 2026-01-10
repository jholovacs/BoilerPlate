# GitHub Actions Workflows

This directory contains GitHub Actions workflows for continuous integration and testing.

## Available Workflows

### 1. `test.yml` - Unit Tests

**Purpose:** Runs unit tests on push and pull requests.

**Features:**
- Restores dependencies
- Builds the solution in Release configuration
- Runs all unit tests
- Collects code coverage using Coverlet
- Generates HTML coverage reports
- Publishes test results to GitHub Actions UI
- Uploads test results and coverage reports as artifacts
- Displays coverage summary in workflow summary

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual workflow dispatch

**Artifacts:**
- `test-results` - Test result files (TRX format) and raw coverage data
- `coverage-report` - HTML coverage reports and summaries

### 2. `ci.yml` - Comprehensive CI Pipeline

**Purpose:** Complete CI pipeline with build, test, and coverage reporting.

**Features:**
- Caches NuGet packages for faster builds
- Restores dependencies
- Builds the solution in Release configuration
- Runs all unit tests with coverage
- Publishes test results with detailed annotations
- Generates comprehensive coverage reports (HTML, Markdown, Cobertura, JSON, Badges)
- Uploads coverage to Codecov (optional, requires Codecov account)
- Adds coverage summary to pull requests
- Uploads all artifacts

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual workflow dispatch

**Artifacts:**
- `test-results-{run_number}` - Test results (TRX) and coverage files
- `coverage-report-{run_number}` - Complete coverage reports

### 3. `build.yml` - Build Verification

**Purpose:** Verifies that the solution builds successfully in both Debug and Release configurations.

**Features:**
- Builds in both Debug and Release configurations
- Caches NuGet packages
- Checks for build warnings

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual workflow dispatch

## Test Framework

The project uses:
- **xUnit** - Testing framework
- **Moq** - Mocking framework
- **FluentAssertions** - Fluent assertions library
- **Coverlet** - Code coverage tool
- **EntityFrameworkCore.InMemory** - In-memory database for testing

## Coverage Reports

Coverage reports are generated in multiple formats:
- **HTML** - Interactive HTML report (best for viewing)
- **Cobertura** - XML format for integration with other tools
- **JSON Summary** - Machine-readable summary
- **Markdown Summary** - Markdown-formatted summary
- **Badges** - Coverage badges for README

## Viewing Results

### Test Results
1. Go to the **Actions** tab in GitHub
2. Select the workflow run
3. Click on the **Test Results** check or job to see detailed test results

### Coverage Reports
1. Go to the **Actions** tab in GitHub
2. Select the workflow run
3. Scroll to the **Artifacts** section at the bottom
4. Download the `coverage-report` artifact
5. Extract and open `index.html` in a web browser

### Workflow Summary
Coverage summaries are automatically displayed in the workflow summary. Click on the job name in the Actions tab to view.

## Codecov Integration

The `ci.yml` workflow includes optional Codecov integration. To enable:
1. Sign up for a free account at [codecov.io](https://codecov.io)
2. Add your repository to Codecov
3. The workflow will automatically upload coverage reports

## Customization

### Changing Trigger Branches

Edit the `on:` section in each workflow file:
```yaml
on:
  push:
    branches: [ main, develop, feature/* ]  # Add more branches
  pull_request:
    branches: [ main, develop ]
```

### Changing .NET Version

Edit the `matrix.dotnet-version` in the workflow:
```yaml
strategy:
  matrix:
    dotnet-version: ['8.0.x', '9.0.x']  # Add more versions
```

### Excluding Coverage Patterns

Edit the `DataCollectionRunSettings` section in the test step:
```yaml
-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute="Obsolete,GeneratedCodeAttribute"
-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*Test*]*,[*.Tests]*"
```

## Running Workflows Locally

While GitHub Actions runs automatically, you can test locally using [act](https://github.com/nektos/act):

```bash
# Install act (see https://github.com/nektos/act#installation)
act -l                    # List workflows
act push                  # Run workflows triggered by push
act pull_request          # Run workflows triggered by PR
```

## Troubleshooting

### Tests Fail
- Check the test output in the Actions tab
- Review test artifacts for detailed error messages
- Ensure all dependencies are properly referenced

### Coverage Reports Not Generated
- Verify that the `coverlet.collector` package is included in test projects
- Check that tests are actually running (look for test output)
- Ensure the coverage collector configuration is correct

### Build Failures
- Check the build logs in the Actions tab
- Verify all project references are correct
- Ensure NuGet packages are properly restored
