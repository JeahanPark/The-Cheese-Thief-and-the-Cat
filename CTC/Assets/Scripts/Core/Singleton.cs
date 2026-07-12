// WES에서 이식(2026-07-12)
namespace CTC.Runtime.Core
{
    public abstract class Singleton<T> : IManager where T : class, new()
    {
        private static T s_Instance;

        public static T Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new T();
                }
                return s_Instance;
            }
        }

        public virtual void Init()
        {
        }

        public virtual void Clear()
        {
            s_Instance = null;
        }
    }
}
