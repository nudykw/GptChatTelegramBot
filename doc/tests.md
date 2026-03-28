# Testing Strategy

This document provides information about the testing strategy and provides guidance on where tests are located and what they cover.

## Test Location
All tests are located in the `tests/` directory at the root of the solution.

---

## [ServiceLayer.UnitTests](../tests/ServiceLayer.UnitTests)
- **Objective**: Isolated testing of business logic in the `ServiceLayer`.
- **Technologies**: xUnit, Moq, FluentAssertions.
- **Key Files**:
    - `ChatGptServiceTests.cs`: Tests for model selection, price calculation, and response handling for OpenAI.
    - `ChatGptPriceUpdateTests.cs`: Tests for model pricing updates from remote sources.

---

## [ServiceLayer.IntegrationTests](../tests/ServiceLayer.IntegrationTests)
- **Objective**: Verifying that the application components work together correctly.
- **Technologies**: xUnit, Microsoft.AspNetCore.Mvc.Testing.
- **Key Files**:
    - `Fixtures/TestAppFixture.cs`: Sets up the shared environment for integration tests (e.g., database, configuration).
    - `ChatGptPriceIntegrationTests.cs`: Verifying model cost calculation and persistence in the database.
    - `ChatGptServiceTests.cs`: Integration tests for the GPT services.

---

## How to Run Tests
To run all tests in the solution, use the following command from the root directory:
```bash
dotnet test
```
To run tests for a specific project:
```bash
dotnet test tests/ServiceLayer.UnitTests
```
