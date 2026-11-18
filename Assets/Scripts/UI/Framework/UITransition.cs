using System.Collections;
using UnityEngine;

namespace Game.UI
{
    public abstract class UITransition : MonoBehaviour
    {
        public abstract IEnumerator PlayOpen();

        public abstract IEnumerator PlayClose();

        public abstract void Stop();
    }
}
