# Test Suite

This directory contains unit tests for the DotImpose library.

## Running Tests

```bash
dotnet test
```

Or run from the repository root:

```bash
dotnet test tests/sillsdev.dotImpose.Tests.csproj
```

## Test Coverage

The test suite includes:

- **NullLayoutMethod tests**: Verifies that PDFs can be passed through unchanged, and that bleed margins are properly applied to TrimBox/ArtBox.
- **SideFoldBookletLayouter tests**: Confirms that side-fold booklet layouts produce valid output.
- **SideFold4UpBookletLayouter tests**: Tests the 4-up booklet layout method enablement.
- **CalendarLayouter tests**: Validates calendar layout generation.

All tests use dynamically generated test PDFs (simple A4 pages with rectangles) to avoid external dependencies.

## Test Structure

- Tests use xUnit as the testing framework
- Each test class implements `IDisposable` for cleanup of temporary files
- Test PDFs are created programmatically using PdfSharp
- Output files are written to temporary directories that are cleaned up after each test run
