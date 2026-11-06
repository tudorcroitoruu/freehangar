# Collectable progress

`CollectablesManager` is attached to the root of `Assets/Prefabs/GameCanvas.prefab`.
Its Inspector exposes a collection for each `BuriedFind.Kind`, containing a title,
an image (`Sprite`), and a list of `BuriedFind` references. Category images are
currently unassigned; assign the final artwork in the prefab's collection entries.

By default, lists are populated from the same scene at startup and refreshed on
collection, including inactive finds and keys spawned at runtime. Use the component
context menu **Populate lists from scene** to populate a scene instance in Edit Mode.
Disable **Discover Scene Collectables** to use only manually assigned lists.

`BuriedFind.Collected` fires after a successful reward commit and save. The manager
counts unique, nonempty find IDs in the matching list against `PlayerData.foundSecrets`.
Other saved events and barriers do not contribute. Loading a profile does not replay
notifications, and a previously collected find cannot commit its reward again.

`GameplayCanvas.ShowAchievement(Sprite, string, float)` forwards the image and title
to `AchievementsPopup.Show`. For example, a title is `Vehicle keys 2/5`. Notifications
are queued with independent display durations; the popup resumes any remaining
existing warning requests afterward. A missing sprite hides the icon.

Run **Tools > Grain > Validate collectable progress** in Unity for the collectable,
warning popup, money popup, and perk Edit Mode tests. Results are written to
`output/Collectables/tests.xml` (41 passed, 0 failed on 2026-11-06).
