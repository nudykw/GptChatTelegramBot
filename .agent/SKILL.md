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

### How to use these files:
1. **Analyze project structure** before making changes to solution layout or dependencies.
2. **Consult core implementations** to understand cross-project service usage.
3. **Verify test locations** before adding new tests or fixing existing ones.
4. **Follow API patterns** detailed in `api_reference.md` for any new integrations.
