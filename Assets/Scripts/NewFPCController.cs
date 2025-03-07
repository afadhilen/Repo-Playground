    using System.Collections;
    using UnityEngine;
    using UnityEngine.InputSystem;

    [RequireComponent(typeof(CharacterController))]
    public class NewFirstPersonController : MonoBehaviour
    {
        [Header("Input Action References")]
        public InputActionReference moveAction;   // Expected: Vector2 (WASD)
        public InputActionReference lookAction;   // Expected: Vector2 (Mouse Delta)
        public InputActionReference jumpAction;   // Expected: Button (Space)
        public InputActionReference attackAction; // Expected: Button (Mouse Left)

        [Header("Movement Settings")]
        public float speed = 6.0f;
        public float jumpSpeed = 8.0f;
        public float gravity = 20.0f;
        public float mouseSensitivity = 2.0f;
        public float verticalRotationLimit = 80.0f;

        [Header("Player Model")]
        [SerializeField] private GameObject characterModel;

        [Header("Punch Settings")]
        public float punchRange = 2.0f;
        public float punchDamage = 2.0f;

        [Header("Knockback Settings")]
        public float knockbackDurationPlayer = 0.3f;  // Duration of knockback effect
        [SerializeField]
        private float horizontalKnockbackForce = 5.0f;  // Horizontal knockback force (set in Inspector)
        [SerializeField]
        private float verticalKnockbackForce = 2.0f;    // Vertical knockback force (set in Inspector)

        private CharacterController controller;
        private Camera playerCamera;
        private Animator animator;
        private float verticalRotation = 0.0f;

        // Velocity used for normal movement (when not knocked back).
        private Vector3 velocity = Vector3.zero;
        // Flag to indicate if knockback is active.
        private bool isKnockbackActive = false;

        // Input state variables.
        private Vector2 moveInput = Vector2.zero;
        private Vector2 lookInput = Vector2.zero;
        private bool jumpInput = false;
        private bool isAttacking = false;

        void OnEnable()
        {
            moveAction.action.Enable();
            lookAction.action.Enable();
            jumpAction.action.Enable();
            attackAction.action.Enable();

            moveAction.action.performed += OnMove;
            moveAction.action.canceled += OnMove;
            lookAction.action.performed += OnLook;
            lookAction.action.canceled += OnLook;
            jumpAction.action.performed += OnJump;
            attackAction.action.performed += OnAttack;
        }

        void OnDisable()
        {
            moveAction.action.performed -= OnMove;
            moveAction.action.canceled -= OnMove;
            lookAction.action.performed -= OnLook;
            lookAction.action.canceled -= OnLook;
            jumpAction.action.performed -= OnJump;
            attackAction.action.performed -= OnAttack;

            moveAction.action.Disable();
            lookAction.action.Disable();
            jumpAction.action.Disable();
            attackAction.action.Disable();
        }

        void Start()
        {
            controller = GetComponent<CharacterController>();
            animator = characterModel.GetComponent<Animator>();
            playerCamera = Camera.main;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            HandleMouseLook();
            // Disable normal movement when knockback is active.
            if (!isKnockbackActive)
                HandleMovement();
            UpdateAnimations();

        }

        // --- Input Callbacks ---
        void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void OnLook(InputAction.CallbackContext context)
        {
            lookInput = context.ReadValue<Vector2>();
        }

        void OnJump(InputAction.CallbackContext context)
        {
            jumpInput = true;
        }

        void OnAttack(InputAction.CallbackContext context)
        {
            if (!isAttacking) // Prevent spamming attack animation
            {
                isAttacking = true;
                HandlePunch();
                StartCoroutine(AttackCooldown());
            }
        }

        // --- Movement and Look ---
        void HandleMouseLook()
        {
            transform.Rotate(0, lookInput.x * mouseSensitivity, 0);
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -verticalRotationLimit, verticalRotationLimit);
            if (playerCamera != null)
                playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        }

        void HandleMovement()
        {
            Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x) * speed;
            Vector3 desiredHorizontal = move;
            // Update horizontal velocity from input.
            velocity.x = desiredHorizontal.x;
            velocity.z = desiredHorizontal.z;

            if (controller.isGrounded)
            {
                if (jumpInput)
                    velocity.y = jumpSpeed;
                else
                    velocity.y = -1f; // Small downward force to keep grounded.
            }

            velocity.y -= gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            jumpInput = false;
        }

        // --- Punching ---
        void HandlePunch()
        {
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, punchRange))
            {
                Debug.Log("Player hit: " + hit.collider.name);
                if (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Zombie"))
                {
                    ZombieAI targetZombie = hit.collider.GetComponent<ZombieAI>();
                    if (targetZombie != null)
                        targetZombie.TakeDamage(punchDamage, transform);
                }
            }
            else
            {
                Debug.Log("Player hit nothing.");
            }
        }
        IEnumerator AttackCooldown()
        {
            yield return new WaitForSeconds(0.5f); // Adjust attack duration
            isAttacking = false;
        }
        // --- Knockback ---
        // This method starts a coroutine that applies knockback similar to the zombie's calculation.
        public void ApplyKnockback(Vector3 direction)
        {
            StartCoroutine(KnockbackCoroutine(direction));
        }

        IEnumerator KnockbackCoroutine(Vector3 knockbackDir)
        {
            // Separate horizontal and vertical components.
            Vector3 horizontalKnockback = new Vector3(knockbackDir.x, 0f, knockbackDir.z).normalized * horizontalKnockbackForce;
            float verticalVelocity = knockbackDir.y * verticalKnockbackForce;

            float timer = 0f;
            isKnockbackActive = true;

            // Apply knockback for the specified duration.
            while (timer < knockbackDurationPlayer)
            {
                // Apply gravity to vertical velocity.
                verticalVelocity -= gravity * Time.deltaTime;

                // Calculate movement for this frame (both horizontal and vertical).
                Vector3 movement = horizontalKnockback * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime;
                controller.Move(movement);

                timer += Time.deltaTime;
                yield return null;
            }

            // Continue applying gravity and horizontal momentum until the player is grounded.
            while (!controller.isGrounded)
            {
                verticalVelocity -= gravity * Time.deltaTime;
                Vector3 movement = horizontalKnockback * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime;
                controller.Move(movement);
                yield return null;
            }

            isKnockbackActive = false;
        }
        void UpdateAnimations()
        {
            bool isWalking = moveInput.magnitude > 0.1f;

            animator.SetBool("IsWalking", isWalking);
            animator.SetBool("IsAttacking", isAttacking);
        }

        // --- Optional Gizmos for Debugging ---
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + velocity);
        }
    }
