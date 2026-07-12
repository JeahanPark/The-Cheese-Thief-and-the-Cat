// WES에서 이식(2026-07-12) — 네임스페이스 이관(CTC.Runtime.Managers), 베이스 타입은 CTC.Runtime.Core 참조.
// Addressables 2.11.1 API 확인 완료: LoadAssetAsync/WaitForCompletion/LoadResourceLocationsAsync/Release 모두 동일 시그니처.
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using CTC.Runtime.Core;

namespace CTC.Runtime.Managers
{
    public class ResourceManager : Singleton<ResourceManager>
    {
        public T Load<T>(string _path) where T : Object
        {
            return Resources.Load<T>(_path);
        }

        public GameObject Instantiate(string _path, Transform _parent = null)
        {
            GameObject prefab = Load<GameObject>(_path);
            if (prefab == null)
            {
                GameDebug.LogError($"Failed to load prefab at path: {_path}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, _parent);
            instance.name = prefab.name;
            return instance;
        }

        public T LoadAddressable<T>(string _key) where T : Object
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(_key);
            handle.WaitForCompletion();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }
            else
            {
                GameDebug.LogError($"Failed to load addressable asset with key: {_key}");
                return null;
            }
        }

        public GameObject InstantiateAddressable(string _key, Transform _parent = null)
        {
            GameObject prefab = LoadAddressable<GameObject>(_key);
            if (prefab == null)
            {
                GameDebug.LogError($"Failed to load addressable prefab with key: {_key}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, _parent);
            instance.name = prefab.name;
            return instance;
        }

        public void Destroy(GameObject _obj, float _delay = 0f)
        {
            if (_obj == null)
            {
                return;
            }

            if (_delay > 0f)
            {
                Object.Destroy(_obj, _delay);
            }
            else
            {
                Object.Destroy(_obj);
            }
        }
    }
}
