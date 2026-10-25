using System;
using System.Collections.Generic;
using LogicCore.Connections;
using LogicCore.Interfaces;
using LogicCore.Management;

namespace LogicCore.Validation
{
    public static class CircuitValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public bool HasCycle { get; set; }

            public static ValidationResult Success() => new() { IsValid = true };

            public static ValidationResult Fail(string message, bool hasCycle = false) =>
                new() { IsValid = false, Message = message, HasCycle = hasCycle };
        }

        public static ValidationResult CanAddGate(CircuitManager? manager, IGate? gate)
        {
            if (manager == null)
                return ValidationResult.Fail("مدير الدارة غير موجود.");

            var result = ValidateGate(gate);
            if (!result.IsValid)
                return result;

            foreach (var existing in manager.Gates)
            {
                if (existing != null &&
                    string.Equals(existing.Id, gate!.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Fail($"معرّف البوابة مكرر: {gate.Id}.");
                }
            }

            return ValidationResult.Success();
        }

        public static ValidationResult CanConnect(
            CircuitManager? manager, IGate? source, IGate? target, int targetInputIndex)
        {
            if (source == null || target == null)
                return ValidationResult.Fail("أحد طرفي التوصيل غير صالح.");

            // نفحص الدارة مع السلك المقترح، بدون إضافته أو تغيير أي إشارة.
            return ValidateAndOrder(
                manager, new Wire(source, target, targetInputIndex), out _);
        }

        public static ValidationResult TryGetEvaluationOrder(
            CircuitManager? manager, out List<IGate> order)
        {
            return ValidateAndOrder(manager, null, out order);
        }

        private static ValidationResult ValidateGate(IGate? gate)
        {
            if (gate == null)
                return ValidationResult.Fail("لا يمكن إضافة بوابة فارغة.");

            if (string.IsNullOrWhiteSpace(gate.Id))
                return ValidationResult.Fail("معرّف البوابة لا يمكن أن يكون فارغًا.");

            if (gate.Inputs == null)
                return ValidationResult.Fail($"مداخل البوابة {gate.Id} غير مهيأة.");

            return ValidationResult.Success();
        }

        private static ValidationResult ValidateAndOrder(
            CircuitManager? manager, Wire? proposedWire, out List<IGate> order)
        {
            order = new List<IGate>();

            if (manager == null)
                return ValidationResult.Fail("مدير الدارة غير موجود.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var incomingCounts = new Dictionary<IGate, int>(ReferenceEqualityComparer.Instance);
            var outgoing = new Dictionary<IGate, List<IGate>>(ReferenceEqualityComparer.Instance);
            var occupiedInputs = new Dictionary<IGate, HashSet<int>>(ReferenceEqualityComparer.Instance);

            foreach (var gate in manager.Gates)
            {
                var result = ValidateGate(gate);
                if (!result.IsValid)
                    return result;

                if (!ids.Add(gate.Id))
                    return ValidationResult.Fail($"معرّف البوابة مكرر: {gate.Id}.");

                incomingCounts.Add(gate, 0);
                outgoing.Add(gate, new List<IGate>());
                occupiedInputs.Add(gate, new HashSet<int>());
            }

            ValidationResult AddEdge(Wire? wire)
            {
                if (wire == null || wire.SourceGate == null || wire.TargetGate == null)
                    return ValidationResult.Fail("السلك أو أحد طرفيه غير صالح.");

                var source = wire.SourceGate;
                var target = wire.TargetGate;

                // تطابق المعرّف وحده لا يكفي: يجب استخدام كائن البوابة الموجود بالدارة.
                if (!incomingCounts.ContainsKey(source) || !incomingCounts.ContainsKey(target))
                    return ValidationResult.Fail("طرفا السلك يجب أن يكونا بوابتين موجودتين في الدارة نفسها.");

                if (ReferenceEquals(source, target))
                    return ValidationResult.Fail("لا يمكن توصيل البوابة بنفسها؛ هذا يكوّن حلقة.", true);

                if (string.Equals(source.GateType, "LAMP", StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Fail("المصباح لا يملك مخرجًا للإشارة.");

                if (target.Inputs.Length == 0 ||
                    string.Equals(target.GateType, "SWITCH", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Fail("البوابة الهدف لا تملك مدخلًا لاستقبال الإشارة.");
                }

                int index = wire.TargetInputIndex;
                if (index < 0 || index >= target.Inputs.Length)
                    return ValidationResult.Fail($"فهرس المدخل {index} خارج حدود البوابة الهدف.");

                if (!occupiedInputs[target].Add(index))
                {
                    return ValidationResult.Fail(
                        $"المدخل رقم ({index + 1}) متصل بسلك بالفعل. لا يمكن وصل سلك ثانٍ بالمدخل نفسه.");
                }

                // لكل سلك اعتماد مستقل، بما فيه تفرع مصدر واحد إلى مدخلين في البوابة نفسها.
                outgoing[source].Add(target);
                incomingCounts[target]++;
                return ValidationResult.Success();
            }

            foreach (var wire in manager.Wires)
            {
                var result = AddEdge(wire);
                if (!result.IsValid)
                    return result;
            }

            if (proposedWire != null)
            {
                var result = AddEdge(proposedWire);
                if (!result.IsValid)
                    return result;
            }

            // Kahn: الحساب يبدأ بالبوابات التي لا تعتمد على بوابة أخرى.
            var ready = new Queue<IGate>();
            foreach (var gate in manager.Gates)
            {
                if (incomingCounts[gate] == 0)
                    ready.Enqueue(gate);
            }

            while (ready.Count > 0)
            {
                var gate = ready.Dequeue();
                order.Add(gate);

                foreach (var target in outgoing[gate])
                {
                    incomingCounts[target]--;
                    if (incomingCounts[target] == 0)
                        ready.Enqueue(target);
                }
            }

            if (order.Count != manager.Gates.Count)
            {
                order.Clear();
                return ValidationResult.Fail(
                    "توجد حلقة في توصيلات الدارة. المحاكاة الحالية تدعم الدارات بدون حلقات فقط.", true);
            }

            return ValidationResult.Success();
        }
    }
}
