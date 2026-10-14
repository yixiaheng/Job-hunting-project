using System;
using System.Collections.Generic;

namespace Battle.SaveData
{
    /// <summary>
    /// 角色配对界面的站队调整
    /// </summary>
    [Serializable]
    public class BattleFormation
    {
        public const int MaxMembers = 4;

        public List<FormationMember> members = new();

        public bool TryAdd(string unitId, int position)
        {
            if (!IsValidUnitId(unitId) || !IsValidPosition(position))
                return false;

            if (members.Count >= MaxMembers)
                return false;

            if (ContainsUnit(unitId) || ContainsPosition(position))
                return false;

            members.Add(new FormationMember
            {
                unitId = unitId,
                position = position
            });

            return true;
        }

        // 用新角色替换指定站位上的角色
        public bool TryReplace(string newUnitId, int position)
        {
            if (!IsValidUnitId(newUnitId) || !IsValidPosition(position))
                return false;

            FormationMember oldMember = GetMemberAtPosition(position);

            // 如果新角色已经在其他位置，先移除
            members.RemoveAll(member => member.unitId == newUnitId);

            // 移除该站位原来的角色
            members.RemoveAll(member => member.position == position);

            members.Add(new FormationMember
            {
                unitId = newUnitId,
                position = position
            });

            return true;
        }

        public void RemoveAtPosition(int position)
        {
            members.RemoveAll(member => member.position == position);
        }

        public FormationMember GetMemberAtPosition(int position)
        {
            foreach (FormationMember member in members)
            {
                if (member.position == position)
                    return member;
            }

            return null;
        }

        public bool ContainsUnit(string unitId)
        {
            foreach (FormationMember member in members)
            {
                if (member.unitId == unitId)
                    return true;
            }

            return false;
        }

        public bool ContainsPosition(int position)
        {
            foreach (FormationMember member in members)
            {
                if (member.position == position)
                    return true;
            }

            return false;
        }

        public void Clear()
        {
            members.Clear();
        }

        private bool IsValidUnitId(string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId);
        }

        private bool IsValidPosition(int position)
        {
            return position >= 0 && position < MaxMembers;
        }
    }

    [Serializable]
    public class FormationMember
    {
        public string unitId;
        public int position;
    }
}
