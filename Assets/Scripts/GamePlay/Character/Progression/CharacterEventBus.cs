using System;
using Game.Events;

namespace Game.Characters
{
    public interface ICharacterEvent
    {
    }

    public sealed class CharacterEventBus : EventBus<ICharacterEvent>
    {
        public CharacterEventBus(Action<Exception, Type, Delegate> reportException = null, Action<Type, int> reportPublish = null) : base(reportException, reportPublish)
        {
        }
    }

    public readonly struct CharacterChangedEvent : ICharacterEvent
    {
        public string UnitId { get; }
        public CharacterOperation Operation { get; }
        public long Revision { get; }
        public System.Collections.Generic.IReadOnlyList<string> AffectedUnitIds { get; }

        public CharacterChangedEvent(string id, CharacterOperation operation, long revision, string[] affected = null)
        {
            UnitId = id;
            Operation = operation;
            Revision = revision;
            AffectedUnitIds = Array.AsReadOnly(affected == null ? new[] { id } : (string[])affected.Clone());
        }
    }
}
