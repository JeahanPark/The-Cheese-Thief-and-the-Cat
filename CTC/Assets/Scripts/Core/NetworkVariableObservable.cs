using System;
using R3;
using Unity.Netcode;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// NGO의 복제 상태(`NetworkVariable<T>`·`NetworkList<T>`) 변경 이벤트를 R3 스트림으로 잇는
    /// 어댑터 (개편 3b, 2026-07-31 / 리스트 추가 2026-07-31). R3에는 NGO 지원이 없어서 여기서 만든다.
    ///
    /// View가 매 프레임 값을 읽는 대신 **바뀔 때만** 받도록 하는 게 목적이다.
    /// `AsObservableWithCurrent`는 구독 즉시 현재 값을 한 번 흘린다 — 화면이 첫 변화까지
    /// 빈 채로 남는 것을 막는다(HUD는 거의 항상 이쪽을 쓴다).
    /// </summary>
    public static class NetworkVariableObservable
    {
        /// <summary>
        /// 리스트가 바뀌었다는 신호만 흘린다(무엇이 바뀌었는지는 담지 않는다).
        /// 참가자 목록처럼 몇 개 안 되는 목록은 통째로 다시 그리는 편이 델타 추적보다 싸고 안전하다.
        /// </summary>
        public static Observable<Unit> AsChangedObservable<T>(this NetworkList<T> _list)
            where T : unmanaged, IEquatable<T>
        {
            return Observable.FromEvent<NetworkList<T>.OnListChangedDelegate, Unit>(
                h => _ => h(Unit.Default),
                h => _list.OnListChanged += h,
                h => _list.OnListChanged -= h);
        }

        public static Observable<T> AsObservable<T>(this NetworkVariable<T> _variable)
        {
            return Observable.FromEvent<NetworkVariable<T>.OnValueChangedDelegate, T>(
                h => (_, current) => h(current),
                h => _variable.OnValueChanged += h,
                h => _variable.OnValueChanged -= h);
        }

        /// <summary>구독 시점의 현재 값을 먼저 흘린 뒤 변경을 잇는다.</summary>
        public static Observable<T> AsObservableWithCurrent<T>(this NetworkVariable<T> _variable)
        {
            return _variable.AsObservable().Prepend(() => _variable.Value);
        }
    }
}
