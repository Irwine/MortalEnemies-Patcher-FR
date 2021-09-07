using Mutagen.Bethesda.WPF.Reflection.Attributes;
namespace MortalEnemies
{
    public enum AttackCommitment
    {   
        Aucun,
        Original,
        Remix
    }

    public class Settings
    {
        [SettingName("Mode d'engagement")]
        public AttackCommitment CommitmentMode = AttackCommitment.Original;
    }
}
