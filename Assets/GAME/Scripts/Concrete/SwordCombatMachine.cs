using R3;
using UnityEngine;

namespace Combat
{
    [System.Serializable]
    public class SwordCombatMachine : ICombat
    {
        public CombatType CombatType => CombatType.None;

        private readonly Sword _view;
        private readonly Subject<CombatSnapshot> _stream;
        private readonly Subject<CombatTransition> _transitionStream;

        private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;



        public SwordCombatMachine(Sword view,
            Subject<CombatSnapshot> stream,
            Subject<CombatTransition> transitionStream)
        {
            _view = view;
            _stream = stream;
            _transitionStream = transitionStream;
        }


        public void HandleInput(CombatAction action)
        {
        }

        public void OnAnimationFrame(CombatAnimationFrame frame)
        {
        }

        public void OnWeaponCollision(Collider other)
        {
        }

        public void Update(float deltaTime)
        {
        }

        public void End()
        {
        }
    }
}