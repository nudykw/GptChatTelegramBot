# Стратегія тестування

🇺🇸 [English](tests.md) | 🇺🇦 Українська

Цей документ описує стратегію тестування та надає вказівки щодо розташування тестів і того, що вони покривають.

## Розташування тестів
Усі тести знаходяться у директорії `tests/` у кореневій папці рішення.

---

## [ServiceLayer.UnitTests](../tests/ServiceLayer.UnitTests)
- **Мета**: Ізольоване тестування бізнес-логіки у `ServiceLayer`.
- **Технології**: xUnit, Moq, FluentAssertions.
- **Ключові файли**:
    - `ChatGptServiceTests.cs`: Тести вибору моделі, розрахунку вартості та обробки відповідей OpenAI.
    - `ChatGptPriceUpdateTests.cs`: Тести оновлення цін моделей із зовнішніх джерел.

---

## [ServiceLayer.IntegrationTests](../tests/ServiceLayer.IntegrationTests)
- **Мета**: Перевірка коректної взаємодії компонентів застосунку.
- **Технології**: xUnit, Microsoft.AspNetCore.Mvc.Testing.
- **Ключові файли**:
    - `Fixtures/TestAppFixture.cs`: Налаштування спільного середовища для інтеграційних тестів (наприклад, база даних, конфігурація).
    - `ChatGptPriceIntegrationTests.cs`: Перевірка розрахунку вартості моделей та збереження у базі даних.
    - `ChatGptServiceTests.cs`: Інтеграційні тести GPT-сервісів.

---

## Як запустити тести
Для запуску всіх тестів у рішенні виконайте команду з кореневої директорії:
```bash
dotnet test
```
Для запуску тестів конкретного проєкту:
```bash
dotnet test tests/ServiceLayer.UnitTests
```

## Запуск тестів конкретного сервісу
Тести категоризовані за сервісом через атрибути `Trait` у xUnit. Це дозволяє запускати лише тести, що відносяться до сервісу, з яким ви працюєте.

Запустити лише тести для `ChatGptService`:
```bash
dotnet test --filter Service=ChatGptService
```

Запустити лише тести для `ChatServiceFactory`:
```bash
dotnet test --filter Service=ChatServiceFactory
```
