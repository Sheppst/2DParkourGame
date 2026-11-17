using static GameManager;

/// <summary>Point d'arrivee : termine le niveau au contact.</summary>
public class Finish : Entity
{
    /// <summary>Termine le niveau.</summary>
    public override void OnPlayerTouch() => GM.Level.Finish();
}
