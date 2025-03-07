using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ZombieAI : MonoBehaviour
{
    [Header("General Settings")]
    public float health = 20f;
    public Transform player; // Assign via Inspector or find by tag.
    public float groundCheckRayLength = 1f;
    public LayerMask groundLayers;  // Only these layers will be detected as ground

    [Header("Player Model")]
    [SerializeField] private GameObject characterModel;
    private Animator animator;
    public GameObject deathParticlePrefab;

    [Header("Wander Settings")]
    public float wanderRadius = 7f;
    public float wanderPauseDuration = 2f;

    [Header("Detection & Attack Settings")]
    public float proximityAwareness = 35f;
    public float meleeAttackRange = 1.5f;
    public float eyeHeight = 1.5f; // used if not using an empty GameObject as eye

    [Header("Knockback Settings")]
    public float knockbackForce = 5f;             // Horizontal knockback strength
    public float knockbackDuration = 0.3f;          // Duration of knockback effect
    public float verticalKnockbackForce = 1.0f;     // Fixed vertical knockback force
    public float gravity = 20.0f;

    [Header("Attack Settings")]
    public float attackCooldown = 1.0f;  // One attack per second
    private float lastAttackTime = 0f;
    public float verticalKnockbackMultiplier = 0.5f;  // (Used for player attack knockback)

    [Header("Movement Settings")]
    public float wanderSpeed = 2f;
    public float chaseSpeed = 4f;

    private NavMeshAgent agent;
    private Transform currentTarget;
    private bool hasRetargeted = false;

    // When a zombie is signaled by another, signalingSource is set.
    private ZombieAI signalingSource = null;
    // Flag to track if this zombie has already sent a signal.
    private bool hasSignaled = false;

    private enum ZombieState { Wander, Chase }
    private ZombieState currentState = ZombieState.Wander;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        // Set initial speed to wanderSpeed.
        agent.speed = wanderSpeed;
        animator = characterModel.GetComponent<Animator>();
        // Find the player by tag if not assigned.
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        currentTarget = null;
        ChooseNewWanderDestination();
    }

    void Update()
    {
        if (currentState == ZombieState.Wander)
        {
            // When detecting the player via detection (not hit), do NOT signal.
            if (player != null && DetectPlayer())
            {
                // Switch to chase without signaling.
                SwitchToChase(player, signalOthers: false);
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                StartCoroutine(WanderPause());
            }
        }
        else if (currentState == ZombieState.Chase)
        {
            if (currentTarget != null)
            {
                agent.SetDestination(currentTarget.position);

                // For zombies that did NOT receive a signal, drop chase if line of sight is lost or out of range.
                if (signalingSource == null)
                {
                    if (!HasLineOfSightTo(currentTarget) ||
                        Vector3.Distance(GetEyePosition(), currentTarget.position) > proximityAwareness)
                    {
                        SwitchToWander();
                        return;
                    }
                }
                // For zombies that received a signal, only drop chase if their signaling source drops chase.
                else
                {
                    if (signalingSource.currentState == ZombieState.Wander)
                    {
                        SwitchToWander();
                        return;
                    }
                }

                if (Vector3.Distance(transform.position, currentTarget.position) <= meleeAttackRange)
                {
                    Attack();
                }
            }
            else
            {
                SwitchToWander();
            }
        }
        UpdateAnimations();
    }

    // Calculates the zombie's eye position.
    Vector3 GetEyePosition()
    {
        return transform.position + Vector3.up * eyeHeight;
    }

    // Checks for line of sight from this zombie to the target.
    bool HasLineOfSightTo(Transform target)
    {
        Vector3 eyePos = GetEyePosition();
        Vector3 targetEyePos = target.position + Vector3.up * eyeHeight;
        RaycastHit hit;
        if (Physics.Raycast(eyePos, (targetEyePos - eyePos).normalized, out hit, proximityAwareness))
        {
            return (hit.transform == target);
        }
        return false;
    }

    // Detection check (without signaling) from zombie's eye to player's eye.
    bool DetectPlayer()
    {
        Vector3 eyePos = GetEyePosition();
        Vector3 playerEyePos = player.position + Vector3.up * eyeHeight;
        float distance = Vector3.Distance(eyePos, playerEyePos);

        if (distance <= proximityAwareness)
        {
            RaycastHit hit;
            if (Physics.Raycast(eyePos, (playerEyePos - eyePos).normalized, out hit, proximityAwareness))
            {
                if (hit.transform == player)
                    return true;
            }
        }
        return false;
    }

    IEnumerator WanderPause()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(wanderPauseDuration);
        agent.isStopped = false;
        ChooseNewWanderDestination();
    }

    void ChooseNewWanderDestination()
    {
        Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
        randomDir.y = 0; // Keep movement horizontal.
        Vector3 destination = transform.position + randomDir;
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(destination, out navHit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
    }

    // Switches the zombie to chase.
    // If signalOthers is true, it sends a signal to nearby wandering zombies,
    // provided it hasn't already sent one or received one.
    void SwitchToChase(Transform target, bool signalOthers)
    {
        currentState = ZombieState.Chase;
        currentTarget = target;
        hasRetargeted = false;
        agent.isStopped = false;
        agent.speed = chaseSpeed;


        if (signalOthers && !hasSignaled && signalingSource == null)
        {
            SignalNearbyZombies(target);
            hasSignaled = true;
        }
    }

    // Resets chase state.
    void SwitchToWander()
    {
        currentState = ZombieState.Wander;
        currentTarget = null;
        hasRetargeted = false;
        agent.speed = wanderSpeed;

        hasSignaled = false;
        signalingSource = null;
        ChooseNewWanderDestination();
    }

    // Attack method with a cooldown of one attack per second.
    void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;
        Debug.Log("Zombie attacks " + currentTarget.name);

        // Calculate the knockback direction for the player (from zombie to player).
        Vector3 knockbackDir = (player.position - transform.position).normalized;
        knockbackDir.y += verticalKnockbackMultiplier;
        knockbackDir.Normalize();

        NewFirstPersonController playerController = player.GetComponent<NewFirstPersonController>();
        if (playerController != null)
        {
            playerController.ApplyKnockback(knockbackDir);
        }
        else
        {
            Debug.LogWarning("PlayerController component not found on player.");
        }
    }

    // When the zombie takes damage, it always sends a signal if it hasn't already signaled or received one.
    public void TakeDamage(float damage, Transform attacker)
    {
        health -= damage;

        // Calculate horizontal knockback direction (ignore vertical).
        Vector3 knockbackDir = transform.position - attacker.position;
        knockbackDir.y = 0f;
        knockbackDir = knockbackDir.normalized;

        StartCoroutine(ApplyKnockback(knockbackDir));

        if (health <= 0)
        {
            Die();
            return;
        }

        // Always send a signal upon taking damage, if not already sent/received.
        if (!hasSignaled && signalingSource == null)
        {
            SignalNearbyZombies(attacker);
            Debug.Log("Sent signal");
            hasSignaled = true;
        }

        // If already chasing and the attacker is different, retarget once.
        if (currentState == ZombieState.Chase && currentTarget != attacker && !hasRetargeted)
        {
            currentTarget = attacker;
            hasRetargeted = true;
        }
        // If in wander, switch to chase without re-signaling (since we just signaled).
        else if (currentState == ZombieState.Wander)
        {
            SwitchToChase(attacker, signalOthers: false);
        }
    }

    // Knockback coroutine: applies only horizontal knockback (from given direction)
    // and applies a fixed vertical force.
    IEnumerator ApplyKnockback(Vector3 knockbackDir)
    {
        agent.updatePosition = false;

        // Use only horizontal components.
        Vector3 horizontalKnockback = new Vector3(knockbackDir.x, 0f, knockbackDir.z).normalized * knockbackForce;
        // Use a fixed vertical force.
        float verticalVelocity = verticalKnockbackForce;

        float timer = 0f;
        while (timer < knockbackDuration)
        {
            verticalVelocity -= gravity * Time.deltaTime;
            Vector3 movement = horizontalKnockback * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime;
            transform.position += movement;

            timer += Time.deltaTime;
            yield return null;
        }

        while (!IsGrounded())
        {
            verticalVelocity -= gravity * Time.deltaTime;
            Vector3 movement = horizontalKnockback * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime;
            transform.position += movement;
            yield return null;
        }

        agent.Warp(transform.position);
        agent.updatePosition = true;
    }

    // Custom grounded check using a downward raycast.
    bool IsGrounded()
    {
        float rayDistance = groundCheckRayLength;
        return Physics.Raycast(transform.position, Vector3.down, rayDistance, groundLayers);
    }

    void Die()
    {
        Debug.Log("Zombie died.");
        if (deathParticlePrefab != null)
        {
            Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    // Signals nearby wandering zombies to chase the attacker.
    // When a zombie is signaled, its signalingSource is set to this zombie.
    void SignalNearbyZombies(Transform attacker)
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, proximityAwareness);
        Debug.Log(gameObject.name + " signaling nearby zombies for attacker: " + attacker.name + ". Found " + colliders.Length + " colliders in range.");
        foreach (Collider col in colliders)
        {
            if (col.gameObject != gameObject && col.CompareTag("Zombie"))
            {
                ZombieAI other = col.GetComponent<ZombieAI>();
                if (other != null)
                {
                    Debug.Log("Found zombie: " + other.gameObject.name + " with state " + other.currentState);
                    if (other.currentState == ZombieState.Wander && !other.hasSignaled && other.signalingSource == null)
                    {
                        Debug.Log("Signaling zombie: " + other.gameObject.name);
                        other.SwitchToChase(attacker, signalOthers: false);
                        other.signalingSource = this;
                    }
                    else
                    {
                        Debug.Log("Zombie " + other.gameObject.name + " did not meet conditions to be signaled (State: "
                            + other.currentState + ", hasSignaled: " + other.hasSignaled + ", signalingSource: "
                            + (other.signalingSource == null ? "null" : other.signalingSource.gameObject.name) + ")");
                    }
                }
                else
                {
                    Debug.Log("Collider " + col.gameObject.name + " does not have a ZombieAI component.");
                }
            }
        }
    }

    // Optional Gizmos to visualize ranges.
    void OnDrawGizmos()
    {
        // Eye position.
        Gizmos.color = Color.red;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Gizmos.DrawSphere(eyePos, 0.2f);

        // Wander range.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);

        // Proximity awareness (centered at eye position).
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(eyePos, proximityAwareness);

        // Attack range (centered on zombie's position).
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRange);

        // Visualize the ground-check ray.
        float rayDistance = groundCheckRayLength;
        Gizmos.color = Color.cyan;
        Vector3 start = transform.position;
        Vector3 end = transform.position + Vector3.down * rayDistance;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.05f);
    }
    private void UpdateAnimations()
    {
        if (!animator) return;

        // "IsWalking" if the agent is moving and not stopped
        bool isWalking = !agent.isStopped && agent.velocity.magnitude > 0.1f;

        // "IsAttacking" if close enough to the target to be attacking
        bool isAttacking = false;
        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            isAttacking = (distance <= meleeAttackRange);
        }

        // Set the booleans for the animator
        animator.SetBool("IsWalking", isWalking);
        animator.SetBool("IsAttacking", isAttacking);
    }
}
