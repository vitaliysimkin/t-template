# Інструкції для агента

- Стек: бекенд `src/TTemplate.Api` (.NET 10, minimal API, SQLite), UI `src/TTemplate.UI` (Vue 3 + Vite + `@vitaliysimkin/t-components`). Структуру описано в `README.md`.
- UI: компоненти t-components (`TButton`, `TTable`, `TCard`…) і лише токени `--t-*` у стилях; стилі `.t-*` не перевизначати. Сторінка — `<section class="page">` + `.page-head`.
- Тексти інтерфейсу й коментарі пиши українською.
- Перевірка після змін: `dotnet build src/TTemplate.Api` і `npm run build --prefix src/TTemplate.UI` (включає vue-tsc).
- Після змін коду додай рядок у `CHANGELOG.md` → `## [Unreleased]`: коротко, з погляду користувача.
- Комітити — лише коли попросять; `CHANGELOG.md` іде в тому ж коміті.
