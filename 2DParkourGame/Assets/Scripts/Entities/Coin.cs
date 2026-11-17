using static GameManager;

/// <summary>Piece : disparait (desactivee, pas detruite) au contact et s'ajoute au compteur de l'EntityManager.</summary>
public class Coin : Entity
{
    /// <summary>Ramasse la piece.</summary>
    public override void OnPlayerTouch() => GM.Entities.Collect(this);
}
