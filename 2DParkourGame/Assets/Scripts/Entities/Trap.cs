using static GameManager;

/// <summary>Piege (pics) : tue le joueur au contact.</summary>
public class Trap : Entity
{
    /// <summary>Tue le joueur.</summary>
    public override void OnPlayerTouch() => GM.Player.Death.Kill(this);
}
