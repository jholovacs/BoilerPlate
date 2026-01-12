# GitHub Actions Workflows

## Code Coverage and Statistics

The `code-coverage.yml` workflow automatically runs on pushes and pull requests to assess code coverage and generate code statistics.

### What it does:

1. **Runs all unit tests** with code coverage collection using Coverlet
2. **Generates coverage reports** in multiple formats (HTML, JSON, Cobertura, LCOV) using ReportGenerator
3. **Calculates code statistics** using `cloc` (Count Lines of Code):
   - Total lines of code
   - Total number of files
   - Total comments
   - Total blank lines
   - Language breakdown
4. **Uploads artifacts** containing:
   - HTML coverage report (viewable in browser)
   - Coverage data in multiple formats
   - Code statistics JSON
5. **Comments on pull requests** with a summary of coverage and statistics
6. **Adds a summary** to the GitHub Actions run with key metrics

### Artifacts:

After each run, you can download:
- `coverage-report/` - HTML coverage report and coverage data files
- `code-stats.json` - Detailed code statistics in JSON format

### Viewing Coverage:

1. Go to the Actions tab in GitHub
2. Click on a workflow run
3. Download the `coverage-report` artifact
4. Extract and open `index.html` in a browser to view the detailed coverage report

### Coverage Thresholds:

The workflow doesn't enforce coverage thresholds, but you can add them if needed. The coverage badge color is determined as:
- Green: ≥ 80%
- Yellow: ≥ 60%
- Red: < 60%
