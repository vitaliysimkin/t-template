# t-template

Шаблон для pet-project на основі Bombadil: оболонка з бічною панеллю (TSidebar, згортання, картка користувача, мобільна TBottomNav),
налаштування на атрибутах (вікно й сторінка, пошук, «Зберегти все»), тема, t-components з npm, бекенд .NET 10 + SQLite.

Необовʼязкові фічі вмикаються при створенні застосунку:

| Фіча | Що дає |
|------|--------|
| `tickets` | Заявки: плаваюча кнопка (Ctrl+Shift+E), вибір елемента, знімок сторінки, варіанти від Claude, обговорення, погодження; скіл `/tickets` |
| `jobs` | Операції: фонові задачі з кроками й логом, історія в SQLite, лічильник активних у меню, drawer з деталями |

## Створити застосунок

```powershell
./new-app.ps1 -Name MyPet
```

Скрипт спитає, які фічі вмикати. Можна й одразу:

```powershell
./new-app.ps1 -Name MyPet -Features tickets,jobs -Path D:\projects\my-pet -Verify
```

- `-Name` — у PascalCase: `TTemplate` → `MyPet`, `t-template` → `my-pet`;
- `-Features` — `tickets,jobs` | `all` | `none`; не вказано — скрипт питає;
- `-Path` — типово сусідня тека `..\<name-in-kebab>`;
- `-NoInstall` — без `npm install`; `-Verify` — ще й `dotnet build` і `npm run build`.

Результат — новий git-репозиторій (без коміту) з README під вибрані фічі.

## Як влаштовано вмикання фіч

- Кожна фіча лежить у своїх теках (`template.json` → `paths`), і для невибраної фічі скрипт їх видаляє.
- Точки підключення обгорнуті маркерами: `// #feature:<id>` … `// #end` (у Markdown — HTML-коментарі). Блоки невибраних фіч скрипт
  вирізає, а для вибраних прибирає тільки маркери. Маркери є в `src/TTemplate.Api/Program.cs`, `src/TTemplate.UI/src/features/index.ts` і `README.md`.
- Нова фіча шаблону: теки + запис у `template.json` + маркери в точках підключення.

## Розробка самого шаблону

Шаблон — робочий застосунок з усіма фічами: запускай його, як описано в `README.md`. Перед комітом перевір обидва крайні варіанти:

```powershell
./new-app.ps1 -Name FullCheck -Features all -Path $env:TEMP\full-check -Verify
./new-app.ps1 -Name MinCheck -Features none -Path $env:TEMP\min-check -Verify
```
