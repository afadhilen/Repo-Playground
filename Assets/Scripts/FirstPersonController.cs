using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float mouseSensitivity = 2.0f;
    public float verticalRotationLimit = 80.0f;

    [Header("Punch Settings")]
    public float punchRange = 2.0f;
    public float punchDamage = 2.0f;

    [Header("Knockback Settings")]
    public float knockbackDecay = 5.0f; // How fast knockback influence fades

    private CharacterController controller;
    private Camera playerCamera;
    private float verticalRotation = 0.0f;

    // Custom velocity for handling gravity, jumping, and knockback
    private Vector3 velocity = Vector3.zero;
    private bool isKnockbackActive = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = Camera.main;

        // Lock the cursor for a first-person experience.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandlePunch();
    }

    // Mouse look handling: horizontal rotation applies to the player, vertical to the camera.
    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player horizontally
        transform.Rotate(0, mouseX, 0);

        // Vertical rotation (clamped)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalRotationLimit, verticalRotationLimit);
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    // Movement handling: processes input for walking, jumping, and applies gravity and knockback.
    void HandleMovement()
    {
        // Get movement input (WASD)
        float moveForward = Input.GetAxis("Vertical");
        float moveSide = Input.GetAxis("Horizontal");
        Vector3 moveInput = (transform.forward * moveForward + transform.right * moveSide);
        Vector3 desiredHorizontal = moveInput * speed;

        // Preserve vertical velocity; update horizontal components.
        velocity.x = desiredHorizontal.x;
        velocity.z = desiredHorizontal.z;

        // Check if grounded for jumping.
        if (controller.isGrounded)
        {
            if (Input.GetButtonDown("Jump"))
            {
                velocity.y = jumpSpeed;
            }
            else if (!isKnockbackActive)
            {
                // Ensure a small downward force to keep controller grounded.
                velocity.y = -1f;
            }
        }

        // Apply gravity continuously.
        velocity.y -= gravity * Time.deltaTime;

        // If knockback is active, gradually blend horizontal movement back to normal input.
        if (isKnockbackActive)
        {
            velocity.x = Mathf.Lerp(velocity.x, desiredHorizontal.x, knockbackDecay * Time.deltaTime);
            velocity.z = Mathf.Lerp(velocity.z, desiredHorizontal.z, knockbackDecay * Time.deltaTime);

            // If nearly aligned with input, end knockback.
            if (Mathf.Abs(velocity.x - desiredHorizontal.x) < 0.1f &&
                Mathf.Abs(velocity.z - desiredHorizontal.z) < 0.1f)
            {
                isKnockbackActive = false;
            }
        }

        controller.Move(velocity * Time.deltaTime);
    }

    // Punching: On Mouse1 press, cast a ray forward from the center of the screen.
    void HandlePunch()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, punchRange))
            {
                Debug.Log("Player hit: " + hit.collider.name);
                // Check if the hit object is an enemy (for example, tagged "Enemy" or "Zombie")
                if (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Zombie"))
                {
                    hit.collider.SendMessage("TakeDamage", punchDamage, SendMessageOptions.DontRequireReceiver);
                }
            }
            else
            {
                Debug.Log("Player hit nothing.");
            }
        }
    }

    // Public method to apply knockback to the player.
    // 'force' is a vector representing the knockback direction and magnitude.
    public void ApplyKnockback(Vector3 force)
    {
        velocity += force;
        isKnockbackActive = true;
    }

    // Optional: Visualize the current velocity vector when selected in the Scene view.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + velocity);
    }
}
