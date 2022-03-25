using System;
using UnityEngine;

public class Enemy : GameBehavior {

    [SerializeField] Transform model = default;

    [SerializeField] EnemyAnimationConfig animationConfig = default;

    private EnemyFactory originFactory;

    public EnemyFactory OriginFactory {
        get => originFactory;
        set {
            Debug.Assert(originFactory == null, "Redefined origin factory!");
            originFactory = value;
        }
    }

    public float Scale { get; private set; }

    private GameTile tileFrom, tileTo;
    private Vector3 positionFrom, positionTo;
    private float progress, progressFactor;
    private Direction direction;
    private DirectionChange directionChange;
    private float directionAngleFrom, directionAngleTo;
    private float speed;
    private float pathOffset;

    private float Health;
    private ParticleSystem deathEffectPrefab;
    private EnemyAnimator animator;

    private void Awake() {
        animator.Configure(
            model.GetChild(0).gameObject.AddComponent<Animator>(),
            animationConfig
        );
    }

    public void Initialize(float scale, float speed, float pathOffset, float health, ParticleSystem deathEffectPrefab) {
        model.localScale = new Vector3(scale, scale, scale);
        this.Scale = scale;
        this.speed = speed;
        this.pathOffset = pathOffset;
        this.Health = health;
        this.deathEffectPrefab = deathEffectPrefab;
        animator.Play();
    }

    public void ApplyDamage(float damage) {
        Debug.Assert(damage >= 0f, "Negative damage applied!");
        Health -= damage;
    }

    public override bool GameUpdate() {
        if (Health <= 0f) {
            Game.EnemyDied(this);
            ParticleSystem deathSystem = Instantiate(deathEffectPrefab, transform.localPosition, Quaternion.identity);
            deathSystem.transform.localScale = this.Scale * Vector3.one;
            ParticleSystem.EmissionModule emission = deathSystem.emission;
            emission.rateOverTime = this.Scale * emission.rateOverTimeMultiplier;
            deathSystem.GetComponent<ParticleSystem>();
            deathSystem.GetComponent<ParticleSystemRenderer>().material.color = model.gameObject.GetComponentsInChildren<Renderer>()[0].material.color;
            Recycle();
            return false;
        }
        progress += Time.deltaTime * progressFactor;
        while (progress >= 1f) {
            if (tileTo == null) {
                Game.EnemyReachedDestination();
                Recycle();
                return false;
            }
            progress = (progress - 1f) / progressFactor;
            PrepareNextState();
            progress *= progressFactor;
        }
        if (directionChange == DirectionChange.None) {
            transform.localPosition = Vector3.Lerp(positionFrom, positionTo, progress);
        }
        else {
            float angle = Mathf.LerpUnclamped(directionAngleFrom, directionAngleTo, progress);
            transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
        return true;
    }

    public override void Recycle() {
        OriginFactory.Reclaim(this);
        animator.Stop();
    }

    internal void spawnOn(GameTile tile) {
        Debug.Assert(tile.NextTileOnPath != null, "Nowhere to go!", this);
        tileFrom = tile;
        tileTo = tile.NextTileOnPath;
        progress = 0f;
        PrepareIntro();
    }

    void PrepareIntro() {
        positionFrom = tileFrom.transform.localPosition;
        positionTo = tileFrom.ExitPoint;
        direction = tileFrom.PathDirection;
        directionChange = DirectionChange.None;
        directionAngleFrom = direction.GetAngle();
        directionAngleTo = direction.GetAngle();
        transform.localRotation = direction.GetRotation();
        progressFactor = 2f * speed;
    }

    void PrepareNextState() {
        tileFrom = tileTo;
        tileTo = tileTo.NextTileOnPath;
        positionFrom = positionTo;
        if (tileTo == null) {
            PrepareOutro();
            return;
        }
        positionTo = tileFrom.ExitPoint;
        directionChange = direction.GetDirectionChangeTo(tileFrom.PathDirection);
        direction = tileFrom.PathDirection;
        directionAngleFrom = directionAngleTo;
        switch (directionChange) {
            case DirectionChange.None: PrepareForward(); break;
            case DirectionChange.TurnRight: PrepareTurnRight(); break;
            case DirectionChange.TurnLeft: PrepareTurnLeft(); break;
            default: PrepareTurnAround(); break;
        }
    }

    void PrepareForward() {
        transform.localRotation = direction.GetRotation();
        directionAngleTo = direction.GetAngle();
        model.localPosition = new Vector3(pathOffset, 0f);
        progressFactor = speed;
    }

    void PrepareTurnRight() {
        directionAngleTo = directionAngleFrom + 90f;
        model.localPosition = new Vector3(pathOffset - 0.5f, 0f);
        transform.localPosition = positionFrom + direction.GetHalfVector();
        progressFactor = speed / (Mathf.PI * 0.5f * (0.5f - pathOffset));
    }

    void PrepareTurnLeft() {
        directionAngleTo = directionAngleFrom - 90f;
        model.localPosition = new Vector3(pathOffset + 0.5f, 0f);
        transform.localPosition = positionFrom + direction.GetHalfVector();
        progressFactor = speed / (Mathf.PI * 0.5f * (0.5f + pathOffset));
    }

    void PrepareTurnAround() {
        directionAngleTo = directionAngleFrom + (pathOffset < 0f ? 180f : -180f);
        model.localPosition = new Vector3(pathOffset, 0f);
        transform.localPosition = positionFrom;
        progressFactor = speed / (Mathf.PI * Mathf.Max(Mathf.Abs(pathOffset), 0.2f));
    }

    void PrepareOutro() {
        positionTo = tileFrom.transform.localPosition;
        directionChange = DirectionChange.None;
        directionAngleTo = direction.GetAngle();
        model.localPosition = new Vector3(pathOffset, 0f);
        transform.localRotation = direction.GetRotation();
        progressFactor = 2f * speed;
    }
}
