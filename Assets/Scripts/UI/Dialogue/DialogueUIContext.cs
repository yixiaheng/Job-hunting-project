using System;
using System.Collections.Generic;
using Game.UI;

namespace DialogueSystem
{
    public sealed class DialogueUIContext : IUIContext
    {
        public DialogueRunner Runner { get; }
        public DialogueLine Line { get; }
        private readonly Action<DialogueUIContext> closed;

        public DialogueUIContext(DialogueRunner runner, DialogueLine line, Action<DialogueUIContext> closed = null)
        {
            Runner = runner != null ? runner : throw new ArgumentNullException(nameof(runner));
            this.closed = closed;
            // 对话推进会复用选项列表，延后打开面板时仍应保留本行的数据。
            Line = new DialogueLine(line.Node, line.Choices != null
                ? new List<DialogueChoice>(line.Choices).AsReadOnly()
                : Array.Empty<DialogueChoice>());
        }

        internal void NotifyClosed() => closed?.Invoke(this);
    }

    public sealed class DialogueInteractionsUIContext : IUIContext
    {
        public IReadOnlyList<DialogueTrigger> Targets { get; }
        public DialogueTrigger Selected { get; }

        public DialogueInteractionsUIContext(DialogueRunner runner)
        {
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));

            Targets = new List<DialogueTrigger>(runner.dialogues).AsReadOnly();
            Selected = runner.CurrentTarget;
        }
    }
}
