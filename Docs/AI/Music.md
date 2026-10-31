# Music playback

`AudioManager` owns the music source. Its regular playlist contains Ambient, Happy, Mexico, and Relax. It randomly picks a different track when one finishes. After a track, there is a 35% chance of a 15 second silent pause before the next song. The chance and duration are serialized on the AudioManager prefab.

The intro cutscene calls `AudioManager.PlayIntroOnce()` when it begins. Intro music plays once per AudioManager lifetime, then regular music starts when the full intro clip finishes. If the intro cutscene is skipped, regular music plays without the intro track. Gameplay has its intro skip flag disabled so the cutscene runs in normal play.

The first rat attack cutscene at Hangar 3 and the first crow attack cutscene at Hangar 4 each start the separate Attack track as soon as the discovery cutscene takes control. Attack is excluded from random selection. After the full Attack track finishes, regular music resumes. Ordinary later pest attacks do not restart Attack music.

Entering `Last Scene` stops the current music and starts Interstellar immediately. Regular music does not resume in that scene. The Last Scene also has its own AudioManager prefab for direct Editor play; when reached from Gameplay, the persistent bootstrapper's AudioManager remains authoritative and the duplicate is discarded.

The source clip, Play On Awake, and Loop settings are cleared on the prefab so only AudioManager selects and starts music. Intro music, Attack, and Interstellar are excluded from regular selection even if accidentally added to the regular array.
