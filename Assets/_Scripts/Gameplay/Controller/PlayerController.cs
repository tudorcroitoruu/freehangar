using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Camera characterCamera;
    public CharacterController characterController;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 3f;
    [SerializeField, Min(0f)] private float sprintSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpForce = 5f;
    [SerializeField, Min(0f)] private float gravity = 9.81f;

    [Header("Camera")]
    [SerializeField, Min(0f)] private float cameraSensitivity = 2f;
    [SerializeField] private float cameraMinAngle = -70f;
    [SerializeField] private float cameraMaxAngle = 80f;

    private float verticalVelocity;
    private float cameraRotationX;

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        characterCamera = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterCamera == null)
            characterCamera = GetComponentInChildren<Camera>();

        if (characterCamera == null)
        {
            Debug.LogError("PlayerController requires a child camera.", this);
            enabled = false;
            return;
        }

        cameraRotationX = Mathf.DeltaAngle(0f, characterCamera.transform.localEulerAngles.x);
    }

    private void OnEnable()
    {
        verticalVelocity = 0f;
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetCursorLocked(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(false);
        else if (Input.GetMouseButtonDown(0))
            SetCursorLocked(true);

        if (!characterController.enabled || Time.deltaTime <= 0f)
            return;

        bool acceptInput = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        if (acceptInput)
            HandleCamera();

        HandleMovement(acceptInput);
    }

    private void HandleMovement(bool acceptInput)
    {
        Vector2 input = acceptInput
            ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
            : Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        float currentSpeed = acceptInput && Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * currentSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (acceptInput && characterController.isGrounded && Input.GetKeyDown(KeyCode.Space))
            verticalVelocity = jumpForce;

        verticalVelocity -= gravity * Time.deltaTime;
        velocity.y = verticalVelocity;
        CollisionFlags collisions = characterController.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -2f;
    }

    private void HandleCamera()
    {
        transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * cameraSensitivity, Space.World);
        cameraRotationX -= Input.GetAxis("Mouse Y") * cameraSensitivity;
        cameraRotationX = Mathf.Clamp(cameraRotationX, cameraMinAngle, cameraMaxAngle);
        characterCamera.transform.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
