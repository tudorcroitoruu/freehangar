from pathlib import Path
p=Path('Assets/_Scripts/Gameplay/Controller/PlayerController.cs');s=p.read_text();s=s.replace('    public bool IsCrouching => isCrouching;','    public bool IsCrouching => isCrouching;\n    public StaminaManager Stamina => staminaManager;');p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Bicycle/PlayerBicycleInteraction.cs');s=p.read_text();a=s.index('            if (Application.isFocused && Time.timeScale > 0 && Input.GetKeyDown(KeyCode.V))');b=s.index('            if (IsAdjustingRidingPosition)',a+100) # first inside shortcut? use exact block instead
block='''            if (Application.isFocused && Time.timeScale > 0 && Input.GetKeyDown(KeyCode.V))
            {
                if (IsAdjustingRidingPosition) EndRidingPositionAdjustment(true);
                else BeginRidingPositionAdjustment();
            }
'''
assert block in s;s=s.replace(block,'');s=s.replace('   V Adjust view','').replace('stamina = player.GetComponent<StaminaManager>()','stamina = player.Stamina').replace('Adjust riding position [V]','Adjust riding position').replace('Bike brakes while adjusting. V also saves and closes.','Bike brakes while adjusting. Save to return to riding.');p.write_text(s)
p=Path('Assets/Editor/BicycleControllerEditor.cs');s=p.read_text().replace('mount the bike and press V or click Adjust riding position.','mount the bike, press Escape and click Adjust riding position.').replace('mount the bike and use V → Save & ride.','mount the bike and click Adjust riding position → Save & ride.');p.write_text(s)
p=Path('output/Bicycle/ValidateBikeAdjustments.cs');s=p.read_text();s=s.replace('typeof(StaminaManager).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stamina,null);','typeof(StaminaManager).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stamina,null);typeof(PlayerController).GetField("staminaManager",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,stamina);');p.write_text(s)
