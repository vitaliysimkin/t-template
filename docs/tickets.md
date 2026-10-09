# Заявки — формат на диску

Процес: **заявка → варіанти або фікс від Claude → обговорення → погодження → застосування**.
Пишуть застосунок (плаваюча кнопка / Ctrl+Shift+E, сторінка `#/tickets`) і Claude (скіл `.claude/skills/tickets`).
Код: `src/TTemplate.Api/Features/Tickets/TicketStore.cs`.

**Головне правило: нічого не затирається.** `thread.md` і `events.jsonl` лише дописуються в кінець; `ticket.json` і `snapshot.html`
пишуться один раз; варіант — новий файл (нова ітерація = новий slug).

## Розташування

Налаштування «Заявки → Тека заявок»; порожнє — `<тека даних>/tickets`. У dev тека даних — `data/` у корені репо, тож заявки лежать у `data/tickets/`.

```
<tickets>/
  <id>/                        id = t-YYYYMMDD-HHMMSS-<4 hex>
    ticket.json                що написав користувач + контекст (маршрут, селектор, компоненти, файл компонента, outerHTML, вікно, тема)
    snapshot.html              знімок сторінки (може не бути)
    thread.md                  обговорення (append)
    events.jsonl               зміни статусу й meta (append)
    variants/<slug>.html       варіанти (пише Claude); slug — [a-z0-9-], до 64 символів: compact-card, bold-title-2
```

## events.jsonl

Один JSON-обʼєкт на рядок (UTF-8, `\n` у кінці). Поля: `ts` (ISO 8601 з поясом), `status`, `by` (`user` | `claude`), `author`,
необовʼязкові `variant`, `text`, `variants` (`[{slug, title, summary?}]` — назви варіантів для `variants-ready`), `scope` (лише в першій `new`).

**Статус = `status` останнього рядка з відомим статусом.**

| status            | хто    | значення |
|-------------------|--------|----------|
| `new`             | user   | щойно створено |
| `awaiting-claude` | user   | користувач дописав коментар до появи варіантів |
| `variants-ready`  | claude | варіанти готові до перегляду |
| `feedback`        | user   | «Ще ітерація» або коментар до варіантів/фіксу |
| `approved`        | user   | погоджено варіант `variant` — Claude застосовує |
| `simple`          | user   | «Дрібне — просто виправ» — Claude застосовує без варіантів |
| `simple-proposed` | claude | Claude описав фікс у thread і чекає `simple`/`feedback` |
| `applied`         | claude | зміни внесено (`text` — що зроблено); чекає перевірки |
| `rejected`        | user   | відхилено |
| `done`            | user   | користувач перевірив і закрив (закриває лише користувач) |

`scope` (у першій події `new`) — мінімум, якого хоче користувач: `fix` або `variants`. Claude може зробити більше, але не менше.

### meta-рядки

Рядок з `"kind":"meta"` і без `status` змінює поля заявки, але не статус. Поля: `severity` (`low` | `medium` | `high` | `none`),
`type` (`feature` | `fix` | `improvement` | `none`, ставить Claude), `caption` (коротка назва, один рядок).
Поточне значення поля береться з останнього meta-рядка, де воно є.

```json
{"ts":"2026-10-01T14:04:00+03:00","kind":"meta","by":"claude","author":"Claude","type":"improvement","caption":"Операції: жирна назва"}
```

## thread.md

Перший рядок — `# Обговорення <id>`. Кожна репліка складається з рядка-заголовка з міткою в HTML-коментарі, порожнього рядка й тексту:

```markdown

### 2026-10-01 14:05:00 +03:00 · claude (Claude) <!-- ticket-msg {"ts":"2026-10-01T14:05:00+03:00","role":"claude","author":"Claude"} -->

Текст репліки…
```

`role` — `user` | `claude`; `variant` — необовʼязково (репліка до конкретного варіанта). Репліка триває до наступного такого заголовка.

## snapshot.html і варіанти

`snapshot.html` — статичний HTML сторінки зі стилями; елемент, до якого заявка, позначено атрибутом `data-ticket-target`.
Варіант — копія знімка, у якій змінено лише стосовну частину. Застосунок показує їх у sandbox-iframe без скриптів (CSP `sandbox`), тож JS у варіантах не виконується.
