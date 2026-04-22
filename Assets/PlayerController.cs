using UnityEngine;
using UnityEngine.InputSystem; // <-- Requisito fundamental para el nuevo sistema

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Input Actions (New Input System)")]
    [Tooltip("Acci�n tipo Value (Vector2) para moverse (WASD/Joystick)")]
    public InputActionReference moveAction;
    [Tooltip("Acci�n tipo Button para correr (Shift)")]
    public InputActionReference sprintAction;
    [Tooltip("Acci�n tipo Button para saltar (Espacio)")]
    public InputActionReference jumpAction;

    [Header("Aceleraci�n y Velocidad")]
    public float walkAcceleration = 50f;
    public float sprintAcceleration = 80f;
    public float maxWalkSpeed = 6f;
    public float maxSprintSpeed = 10f;

    [Header("Salto y Fricci�n")]
    public float jumpForce = 6f;
    public float groundFriction = 6f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    // En el nuevo Input System, es vital suscribirse a los eventos y habilitar las acciones
    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (sprintAction != null) sprintAction.action.Enable();

        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            // Nos suscribimos al evento "performed" para que el salto se ejecute solo al presionar
            jumpAction.action.performed += OnJumpPerformed;
        }
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (sprintAction != null) sprintAction.action.Disable();

        if (jumpAction != null)
        {
            jumpAction.action.Disable();
            // Desuscribimos el evento para evitar memory leaks
            jumpAction.action.performed -= OnJumpPerformed;
        }
    }

    void Update()
    {
        // Leemos continuamente el valor del Vector2 para el movimiento
        if (moveAction != null)
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }

        // IsPressed() devuelve true mientras el bot�n se mantenga apretado
        if (sprintAction != null)
        {
            isSprinting = sprintAction.action.IsPressed();
        }
    }

    void FixedUpdate()
    {
        ApplyMovement();
        ApplyFriction();
    }

    private void ApplyMovement()
    {
        if (moveInput.magnitude == 0) return;

        float currentMaxSpeed = isSprinting ? maxSprintSpeed : maxWalkSpeed;
        float currentAccel = isSprinting ? sprintAcceleration : walkAcceleration;

        Vector3 moveDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // El Dot Product se encarga de limitar la velocidad direccional sin matar el impulso general
        float speedInMoveDirection = Vector3.Dot(currentVelocityXZ, moveDir);

        if (speedInMoveDirection < currentMaxSpeed)
        {
            rb.AddForce(moveDir * currentAccel, ForceMode.Acceleration);
        }
    }

    private void ApplyFriction()
    {
        if (isGrounded && moveInput.magnitude == 0)
        {
            Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 frictionForce = -currentVelocityXZ * groundFriction;
            rb.AddForce(frictionForce, ForceMode.Acceleration);
        }
    }

    // Este m�todo es llamado autom�ticamente por el evento del Action
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Record� que para mayor precisi�n en un juego final, conviene cambiar esto 
        // por un Physics.SphereCast hacia abajo desde los pies del personaje.
        isGrounded = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}