# Figma Frame → Unity

Простой локальный импортер, который переносит выбранные **Frames из Figma в Unity uGUI**.

Всё работает на вашем компьютере через `localhost:38457`. Токен Figma, ссылка на файл или доступ к облаку **не нужны**.

## Что нужно

- Установленный **Unity Editor**.
- Именно **Figma Desktop**. В браузерной Figma локальный dev-плагин может не работать.
- Эта папка `FigmaUnityImporter`.

## 1. Установка в Unity

1. Закройте Unity или остановите Play Mode.
2. Скопируйте **всю папку** `FigmaUnityImporter` в папку `Assets` вашего Unity-проекта.
3. В итоге путь должен выглядеть так:

   ```text
   MyUnityProject/
   └── Assets/
       └── FigmaUnityImporter/
           ├── Editor/
           ├── Runtime/
           └── FigmaPlugin/
   ```

4. Откройте Unity и дождитесь, пока он закончит импорт и компиляцию.
5. В верхнем меню Unity откройте **Tools → Figma Frame Importer**.
6. Если в окне написано `Listening on localhost:38457`, Unity готов.

Bridge обычно запускается сам. Если он не запустился, нажмите **Start local bridge**.

> Важно: не копируйте отдельно только `Editor`, `Runtime` или `FigmaPlugin`. В Unity должна лежать целиком вся папка `FigmaUnityImporter`.

## 2. Установка плагина в Figma

Это нужно сделать **один раз**. Повторная установка не нужна, пока вы не переместили и не удалили папку `FigmaUnityImporter`.

1. Откройте **Figma Desktop**.
2. Откройте любой Figma Design-файл.
3. Откройте меню Figma в левом верхнем углу.
4. Выберите **Plugins → Development → Import plugin from manifest…**.
5. В открывшемся окне найдите ваш Unity-проект и выберите файл:

   ```text
   MyUnityProject/Assets/FigmaUnityImporter/FigmaPlugin/manifest.json
   ```

6. После этого плагин **Figma Frame → Unity** появится в разделе **Development**.

`manifest.json` — это не папка и не файл из Unity с расширением `.meta`. Нужно выбрать именно `manifest.json` без `.meta`.

## 3. Как импортировать дизайн

### Шаг 1. Подготовьте Unity

1. Откройте Unity-проект.
2. Откройте **ту сцену, в которую нужно добавить UI**.
3. Дождитесь окончания компиляции. В Console не должно быть красных ошибок.
4. Проверьте **Tools → Figma Frame Importer**. Должно быть написано `Listening on localhost:38457`.

### Шаг 2. Выберите Frame в Figma

1. В Figma выделите на холсте или в панели Layers один или несколько экранов.
2. Каждый выбранный объект должен иметь тип **Frame**.

Плагин не примет отдельный Text, Rectangle, Group, Section или Component. Если нужный UI лежит в Group, сначала поместите его во Frame.

### Шаг 3. Запустите плагин

1. В Figma откройте **Plugins → Development → Figma Frame → Unity**.
2. Нажмите **Import selected Frames**.
3. Не закрывайте Figma и Unity, пока прогресс не дойдёт до 100% и не появится сообщение `Done`.

Готово. Импортированный UI появится в **Hierarchy активной Unity-сцены**.

## Что создаст Unity

- UI-объекты в активной сцене.
- `Canvas` и `EventSystem`, если их ещё нет.
- Изображения и другие общие ассеты в `Assets/FigmaImports/Shared`.
- Legacy `AnimationClip`-ассеты, если в макете есть поддерживаемая timeline-анимация.

Папку для ассетов можно изменить в поле **Shared asset folder** в окне **Tools → Figma Frame Importer**. Путь должен начинаться с `Assets/`.

## Если не работает

| Проблема | Что делать |
| --- | --- |
| `Select one or more Figma Frames only` | Вы выбрали не Frame или вместе с Frame выбрали другой тип объекта. Снимите выделение и выберите только Frames. |
| `Unity bridge is not listening on localhost:38457` | Откройте Unity, дождитесь конца компиляции, затем откройте **Tools → Figma Frame Importer** и нажмите **Start local bridge**. |
| В Unity нет пункта `Tools → Figma Frame Importer` | Проверьте, что папка лежит в `Assets/FigmaUnityImporter`, и исправьте все красные ошибки в Unity Console. |
| Плагина нет в Figma | Повторите импорт через **Plugins → Development → Import plugin from manifest…** и выберите `manifest.json`, а не `manifest.json.meta`. |
| Импорт долго идёт | Большие Frames и много картинок требуют времени. Не запускайте плагин повторно, пока текущий импорт не завершён. |
| Порт `38457` занят | Закройте другую копию Unity или программу, которая использует этот порт, и нажмите **Start local bridge** ещё раз. |

Если ошибка осталась, посмотрите текст в окне **Tools → Figma Frame Importer** и красные сообщения в **Window → General → Console**.

## Короткая шпаргалка

```text
1. Открыть Unity и нужную сцену.
2. Unity: Tools → Figma Frame Importer.
3. Figma: выбрать только один или несколько Frames.
4. Figma: Plugins → Development → Figma Frame → Unity.
5. Нажать Import selected Frames.
6. Дождаться Done и сохранить Unity-сцену.
```

## Дополнительно

При импорте нескольких Frames плагин передаёт их в Unity по одному, чтобы не забивать память. В Unity отображается прогресс, а импорт можно отменить.

После успешного импорта служебные `FigmaNodeMetadata` автоматически удаляются из UI-объектов активной сцены. Визуальные и интерактивные uGUI-компоненты остаются на месте. Для ручной очистки есть меню **Tools → Remove Figma Scripts**.
