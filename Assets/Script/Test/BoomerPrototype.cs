using System.Collections;
using UnityEngine;

public class BoomerPrototypeController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;


    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float explodeRange = 3f;


    [Header("Health")]
    public float maxHealth = 100f;


    [Header("Warning")]
    public float screamDuration = 1.5f;


    [Header("Explosion")]
    public float explosionDelay = 2f;

    public GameObject explosionPrefab;
    public GameObject acidPoolPrefab;



    private Animator animator;


    private float health;


    private bool canMove = true;
    private bool warningStarted = false;
    private bool dead = false;
    private bool exploded = false;


    private float currentAnimSpeed;



    private enum State
    {
        Chase,
        Warning,
        Death,
        Exploded
    }


    private State state;



    void Awake()
    {
        animator = GetComponent<Animator>();

        health = maxHealth;

        state = State.Chase;
    }



    void Update()
    {
        // TEST DAMAGE
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage(20f);
        }


        UpdateAnimator();


        switch (state)
        {
            case State.Chase:
                Chase();
                break;


            case State.Warning:
                // locked movement
                break;


            case State.Death:
                // waiting for boom
                break;


            case State.Exploded:
                break;
        }
    }




    void UpdateAnimator()
    {
        // Blend Tree only cares about animation speed
        animator.SetFloat(
            "Speed",
            currentAnimSpeed,
            0.2f,
            Time.deltaTime
        );
    }





    void Chase()
    {
        if (target == null)
            return;


        Vector3 direction =
            target.position - transform.position;


        direction.y = 0;



        float distance =
            direction.magnitude;



        if (canMove)
        {
            transform.position +=
                direction.normalized *
                moveSpeed *
                Time.deltaTime;


            transform.forward =
                direction.normalized;


            // keep blend tree alive
            currentAnimSpeed = moveSpeed;
        }



        if (distance <= explodeRange &&
           !warningStarted)
        {
            warningStarted = true;

            StartCoroutine(
                StartWarning()
            );
        }
    }


    IEnumerator StartWarning()
    {
        state = State.Warning;


        // LOCK movement only
        canMove = false;


        // DO NOT touch Speed here
        // let blend tree transition naturally

        animator.SetBool(
            "isWarning",
            true
        );



        float timer = 0;



        while (timer < screamDuration)
        {
            timer += Time.deltaTime;


            animator.SetFloat(
                "Inflate",
                timer / screamDuration
            );


            yield return null;
        }



        StartExplosionDeath();
    }

    void StartExplosionDeath()
    {
        state = State.Death;



        animator.SetBool(
            "isWarning",
            false
        );



        animator.SetTrigger(
            "Explode"
        );



        StartCoroutine(
            ExplosionRoutine()
        );
    }


    IEnumerator HitStun()
    {
        // nếu đang chết / chuẩn bị nổ thì không cần unlock lại
        if (state == State.Death ||
           state == State.Exploded)
            yield break;


        canMove = false;


        yield return new WaitForSeconds(1f);



        // chỉ cho chạy lại nếu vẫn còn sống
        if (!dead && !exploded)
        {
            canMove = true;
        }
    }

    IEnumerator ExplosionRoutine()
    {
        yield return new WaitForSeconds(
            explosionDelay
        );


        Explode();
    }


    public void TakeDamage(float damage)
    {
        if (dead || exploded)
            return;


        health -= damage;


        Debug.Log(
            "BOOMER HIT - HP: " + health
        );


        animator.SetTrigger(
            "Hit"
        );


        // lock movement 1 giây
        StartCoroutine(
            HitStun()
        );



        if (health <= 0)
        {
            dead = true;

            canMove = false;


            Debug.Log(
                "BOOMER DEAD -> EXPLOSION SEQUENCE"
            );


            StartExplosionDeath();
        }
    }


    void Explode()
    {
        if (exploded)
            return;


        exploded = true;


        state = State.Exploded;



        if (explosionPrefab != null)
        {
            Instantiate(
                explosionPrefab,
                transform.position,
                Quaternion.identity
            );
        }




        if (acidPoolPrefab != null)
        {
            Instantiate(
                acidPoolPrefab,
                transform.position,
                Quaternion.identity
            );
        }



        Debug.Log(
            "BOOMER DETONATED"
        );



        Destroy(gameObject);
    }
}