using UnityEngine;
using UnityEngine.InputSystem;

public class FreeFlyCamera : MonoBehaviour
{
    public float velocidad = 1f;
    public float velocidadRapida = 5f;
    public float velocidadRotacion = 60f; // grados por segundo

    private float rotX = 0f;
    private float rotY = 0f;

    void Start()
    {
        Vector3 angulos = transform.eulerAngles;
        rotX = angulos.y;
        rotY = angulos.x;
        if (rotY > 180f) rotY -= 360f;
    }

    void Update()
    {
        // Rotacion con flechas (mientras NO se presiona Ctrl, las flechas mueven; con Ctrl, rotan)
        if (Keyboard.current.leftCtrlKey.isPressed)
        {
            if (Keyboard.current.upArrowKey.isPressed) rotY += velocidadRotacion * Time.deltaTime;
            if (Keyboard.current.downArrowKey.isPressed) rotY -= velocidadRotacion * Time.deltaTime;
            if (Keyboard.current.leftArrowKey.isPressed) rotX -= velocidadRotacion * Time.deltaTime;
            if (Keyboard.current.rightArrowKey.isPressed) rotX += velocidadRotacion * Time.deltaTime;

            rotY = Mathf.Clamp(rotY, -90f, 90f);
            transform.rotation = Quaternion.Euler(rotY, rotX, 0);
        }

        // Movimiento con WASD
        float vel = Keyboard.current.leftShiftKey.isPressed ? velocidadRapida : velocidad;
        Vector3 dir = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) dir += transform.forward;
        if (Keyboard.current.sKey.isPressed) dir -= transform.forward;
        if (Keyboard.current.aKey.isPressed) dir -= transform.right;
        if (Keyboard.current.dKey.isPressed) dir += transform.right;
        if (Keyboard.current.eKey.isPressed) dir += Vector3.up;
        if (Keyboard.current.qKey.isPressed) dir -= Vector3.up;

        transform.position += dir * vel * Time.deltaTime;
    }
}