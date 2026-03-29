---
name: Project Analyzer
description: Guidelines for analyzing the GPTChatTelegramBot repository using official documentation in docs/.
---

# Project Documentation Guidelines

This repository follows a structured documentation approach. Always refer to the following files in the `docs/` folder for analysis:

- **[projects.md](file:///Users/nudyk/Projects/DotNet/GPTChatTelegramBot/docs/projects.md)**: Details of each project in the solution and its purpose.
- **[core_files.md](file:///Users/nudyk/Projects/DotNet/GPTChatTelegramBot/docs/core_files.md)**: Overview of critical classes and core implementations.
- **[tests.md](file:///Users/nudyk/Projects/DotNet/GPTChatTelegramBot/docs/tests.md)**: Information about testing infrastructure and test suite organization.
- **[api_reference.md](file:///Users/nudyk/Projects/DotNet/GPTChatTelegramBot/docs/api_reference.md)**: Details on internal and external API usage (OpenAI, Telegram, etc.).
- **[README.md](file:///Users/nudyk/Projects/DotNet/GPTChatTelegramBot/docs/README.md)**: General documentation overview.

### 🛠️ Mandatory Development Rules:
1. **Actualize Documentation**: After changing any service (especially in `ServiceLayer`), you MUST immediately update the relevant documentation in `docs/` to reflect the changes.
2. **Suggest and Implement Tests**: For every change to business logic or service implementation, you MUST suggest writing new tests or updating existing ones in the `tests/` directory.
3. **Run Relevant Tests Only**: When adding or modifying a service, identify the corresponding `Trait("Service", "ServiceName")` and run only those tests using `dotnet test --filter Service=ServiceName` to optimize execution time and avoid side effects in integration tests.
4. **Communication Language**: Always communicate with the user in Russian. If no localized version exists, keep guidelines, rules, and documentation in English.
5. **Use Constants for Recurring Strings**: If a string literal is used more than once in the codebase, it MUST be extracted into a constant (preferably in `ServiceLayer.Constants` or as a `StaticEnumBase` member).
6. **Use Type-Safe Enums for Entity Values**: For entities having multiple predefined values, use either standard C# `enum` or the `IStaticStringEnum` pattern (via `StaticStringEnumBase`) to ensure type safety and discoverability.
7. **Set Explicit Minimal String Lengths for Database Entities**: All string fields in database models (e.g., in `DataBaseLayer/Models/`) MUST have an explicit, minimal yet sufficient size defined using `[MaxLength(N)]` or `[StringLength(N)]`. If the required size is ambiguous or expected values are unknown, you MUST ask the user for clarification.
8. **Bot Command Synchronization**: If any bot commands are added, modified, or removed in `BotCommands.cs`, or if their descriptions are updated in `README.md`/`README.uk.md`, you MUST immediately synchronize these changes across all README files and the `HelpText` resource in all localization files (`BotMessages.resx`, `BotMessages.uk.resx`, etc.). Ensure that the bot's in-app help matches the documentation, including parameters and their requirements (e.g., `<Required>`, `[Optional]`).
9. **Dynamic Help & Resource Documentation**: All command descriptions in localization resources MUST be documented in `<data name="HelpText" xml:space="preserve">` to ensure consistency with `README.md` and `README.uk.md`.

### How to use these files:
1. **Analyze project structure** before making changes to solution layout or dependencies.
2. **Consult core implementations** to understand cross-project service usage.
3. **Verify test locations** before adding new tests or fixing existing ones.
4. **Follow API patterns** detailed in `api_reference.md` for any new integrations.
5. **Update docs and tests** as per the Mandatory Development Rules above.
