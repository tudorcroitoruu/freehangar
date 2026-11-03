# Goal validation

Unity Edit Mode; Gameplay scene settings with prefab overrides.

Configured 0 missing hangar contexts in the loaded scene without saving/reloading it.

PASS GoalCheckerTests.WrongActionOrHangarDoesNotCount()
PASS GoalCheckerTests.GlobalGrainQuantitiesIgnoreHangarAndKeepGrainFilter(GrainCollected)
PASS GoalCheckerTests.GlobalGrainQuantitiesIgnoreHangarAndKeepGrainFilter(GrainLoaded)
PASS GoalCheckerTests.GlobalGrainQuantitiesIgnoreHangarAndKeepGrainFilter(GrainSold)
PASS GoalCheckerTests.GlobalGrainQuantitiesIgnoreHangarAndKeepGrainFilter(GrainMoved)
PASS GoalCheckerTests.MixedGrainsContributeWhenFilterIsOff()
PASS GoalCheckerTests.SpecificFilterRejectsOtherAndUnknownGrains()
PASS GoalCheckerTests.AnyHangarCountsKnownAndUnknownSources()
PASS GoalCheckerTests.InvalidIncrementsDoNotCount(-1)
PASS GoalCheckerTests.InvalidIncrementsDoNotCount(0)
PASS GoalCheckerTests.InvalidIncrementsDoNotCount(NaN)
PASS GoalCheckerTests.InvalidIncrementsDoNotCount(Infinity)
PASS GoalCheckerTests.ProgressIsCappedAndCompletedGoalsCannotChange()
PASS GoalCheckerTests.FutureSinceActivationGoalIgnoresEarlierActions()
PASS GoalCheckerTests.FutureLifetimeGoalRemembersEarlierActions()
PASS GoalCheckerTests.SackSnapshotsDoNotSumDifferentPartiallyFilledSacks()
PASS GoalCheckerTests.CurrentStateCanDecreaseBeforeActivationAndRejectsDeltas()
PASS GoalCheckerTests.UpgradeMustMatch()
PASS GoalCheckerTests.CustomActionMustIdentifyItsGoal()
PASS GoalCheckerTests.HangarMilestonesRequireStateReportsAndMatchingHangar(HangarKeyCollected)
PASS GoalCheckerTests.HangarMilestonesRequireStateReportsAndMatchingHangar(HangarUnlocked)
PASS GoalCheckerTests.SavedHangarStateKeepsKeyCollectionSeparateFromOpening()
PASS GoalCheckerTests.ClearanceStateRestoresTheSelectedHangarPercentage()
PASS GoalCheckerTests.PurchasedUpgradesRemainCompleteWithoutPickupStock(LargerSack)
PASS GoalCheckerTests.PurchasedUpgradesRemainCompleteWithoutPickupStock(LargerTruck)
PASS GoalCheckerTests.LegacyVehicleAndShovelPurchasesRestoreUpgradeState()
PASS GoalsManagerTests.OneActionDoesNotSpillIntoNextSinceActivationGoal()
PASS GoalsManagerTests.LifetimeMilestonesAdvanceInOrderAndRewardsAreNotGrantedTwiceAfterReload()
PASS GoalsManagerTests.PartialProgressSurvivesJsonRoundTrip()
PASS GoalsManagerTests.ActionsBeforeSaveLoadAreBuffered()
PASS GoalsManagerTests.FutureStateDoesNotSkipEarlierGoal()
PASS GoalsManagerTests.GoalRewardsDoNotCountAsGrainSaleIncome()
PASS GoalsManagerTests.DuplicateIdsAreRejected()
PASS GoalsManagerTests.MissingOldSaveArrayIsInitialized()
PASS GoalCampaignTests.AuthoredGoalsHaveCompatibleGameplayReports()
PASS GoalCampaignTests.ScenePilesKeysAndDoorsHaveMatchingHangarIdentities()
PASS GoalCampaignTests.WholeCampaignCompletesUsingAvailableGrain(False)
PASS GoalCampaignTests.WholeCampaignCompletesUsingAvailableGrain(True)

Results: 38 passed, 0 failed.

## Campaign completion order
1. PASS tutorial_pick_up_bag
2. PASS tutorial_place_bag
3. PASS tutorial_fill_corn_bag
4. PASS tutorial_close_bag
5. PASS tutorial_grab_and_load_bag
6. PASS tutorial_reach_buyer
7. PASS tutorial_sell_corn
8. PASS tutorial_upgrade_shovel
9. PASS upgrade_larger_sack
10. PASS h1_load_200kg
11. PASS h1_earn_500
12. PASS h2_collect_key
13. PASS h1_clear
14. PASS h2_unlock
15. PASS h2_load_2000kg
16. PASS h2_load_5000kg
17. PASS p_sell_8500
18. PASS upgrade_larger_truck
19. PASS h3_collect_key
20. PASS h2_clear
21. PASS h3_unlock
22. PASS p_sell_14000
23. PASS h3_load_15000kg
24. PASS h3_load_20000kg
25. PASS h3_clear
26. PASS h4_collect_key
27. PASS h4_unlock
28. PASS p_sell_26000
29. PASS h4_load_35000kg
30. PASS h4_sell_soybeans_5000kg
31. PASS h4_clear_98
32. PASS h5_collect_key
33. PASS h5_unlock
34. PASS h5_sell_rice_10000kg
35. PASS h5_load_55000kg
36. PASS h5_load_65000kg
37. PASS h5_find_out_what_is_behind_Door

This is a campaign logic simulation. It does not certify camera reachability, collision, key exposure, vehicle handling, or visual/UI behavior in Play Mode.
