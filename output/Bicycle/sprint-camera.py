from pathlib import Path
p=Path('Assets/_Scripts/Gameplay/Controller/PlayerController.cs');s=p.read_text();s=s.replace('    [Header("Crouch (hold Ctrl or C)")]','''    [Header("Sprint camera motion")]
    [SerializeField, Range(0f, .04f)] private float sprintCameraHeight = .012f;
    [SerializeField, Range(0f, 2f)] private float sprintCameraPitch = .45f;
    [SerializeField, Range(1f, 4f)] private float sprintStepFrequency = 2.6f;
    private float sprintCameraPhase, sprintCameraBlend;

    [Header("Crouch (hold Ctrl or C)")]''');s=s.replace('''        verticalVelocity = 0;
    }

    private void HandleGrainScooped()''','''        verticalVelocity = 0;
        sprintCameraBlend = sprintCameraPhase = 0;
    }

    public void ClearSprintCameraMotion()
    {
        sprintCameraBlend = sprintCameraPhase = 0;
        if (characterCamera == null || characterController == null) return;
        characterCamera.transform.localPosition = cameraRestPosition - Vector3.up * Mathf.Max(0, standingHeight - characterController.height);
        characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch, 0, 0);
    }

    private void UpdateSprintCamera(bool sprinting, float dt)
    {
        sprintCameraBlend = Mathf.Lerp(sprintCameraBlend, sprinting ? 1 : 0, 1f - Mathf.Exp(-8f * dt));
        if (sprintCameraBlend > .0001f)
            sprintCameraPhase = Mathf.Repeat(sprintCameraPhase + dt * sprintStepFrequency * Mathf.PI * 2, Mathf.PI * 2);
        Vector3 baseline = cameraRestPosition - Vector3.up * Mathf.Max(0, standingHeight - characterController.height);
        characterCamera.transform.localPosition = baseline + Vector3.up * (Mathf.Sin(sprintCameraPhase) * sprintCameraHeight * sprintCameraBlend);
        characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch + Mathf.Cos(sprintCameraPhase) * sprintCameraPitch * sprintCameraBlend, 0, 0);
    }

    private void HandleGrainScooped()''');s=s.replace('''        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -2f;
    }''','''        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -2f;
        Vector3 actualMovement = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up);
        bool sprinting = acceptInput && Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > .01f &&
            !isCrouching && characterController.isGrounded && actualMovement.sqrMagnitude > .25f;
        UpdateSprintCamera(sprinting, Time.deltaTime);
    }''',1);p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Bicycle/PlayerBicycleInteraction.cs');s=p.read_text();s=s.replace('        cameraParent=view.transform.parent;','        player.ClearSprintCameraMotion();\n        cameraParent=view.transform.parent;',1);p.write_text(s)
