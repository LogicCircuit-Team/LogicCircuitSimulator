using System;
using System.Collections.Generic;
using LogicCore.Connections;
using LogicCore.Interfaces;
using LogicCore.Validation;
using ValidationResult = LogicCore.Validation.CircuitValidator.ValidationResult;

namespace LogicCore.Management
{
    public class CircuitManager
    {
        // أبقينا نوع القائمتين متوافقًا مع باقي المشروع.
        // استخدم دوال المدير للتعديل؛ ويُعاد فحص كامل البيانات قبل كل محاكاة.
        public List<IGate> Gates { get; } = new List<IGate>();
        public List<Wire> Wires { get; } = new List<Wire>();

        public bool HasFeedbackLoop { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public void AddGate(IGate gate)
        {
            var result = TryAddGate(gate);
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
        }

        public ValidationResult TryAddGate(IGate? gate)
        {
            var result = CircuitValidator.CanAddGate(this, gate);
            if (!result.IsValid)
                return RecordResult(result);

            Gates.Add(gate!);
            return RecordResult(ValidationResult.Success());
        }

        public void AddWire(Wire wire)
        {
            var result = TryAddWire(wire);
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
        }

        public ValidationResult TryAddWire(Wire? wire)
        {
            if (wire == null)
                return RecordResult(ValidationResult.Fail("لا يمكن إضافة سلك فارغ."));

            var result = CircuitValidator.CanConnect(
                this, wire.SourceGate, wire.TargetGate, wire.TargetInputIndex);

            if (!result.IsValid)
                return RecordResult(result);

            Wires.Add(wire);
            return RecordResult(ValidationResult.Success());
        }

        public void RemoveGate(IGate gate)
        {
            if (gate == null || !Gates.Remove(gate))
                return;

            Wires.RemoveAll(w => w != null &&
                (ReferenceEquals(w.SourceGate, gate) || ReferenceEquals(w.TargetGate, gate)));

            // يُعاد حساب كل النتائج، بما فيها البوابات التي كانت تتلقى الإشارة المحذوفة.
            TrySimulate();
        }

        public void RemoveWire(Wire wire)
        {
            if (wire == null || !Wires.Remove(wire))
                return;

            TrySimulate();
        }

        public void Clear()
        {
            foreach (var gate in Gates)
            {
                if (gate?.Inputs != null)
                    Array.Clear(gate.Inputs, 0, gate.Inputs.Length);
            }

            Wires.Clear();
            Gates.Clear();
            HasFeedbackLoop = false;
            LastError = string.Empty;
        }

        // التوقيع القديم باقٍ حتى تعمل استدعاءات باقي المكونات كما هي.
        public void Simulate()
        {
            TrySimulate();
        }

        public ValidationResult TrySimulate()
        {
            var result = CircuitValidator.TryGetEvaluationOrder(this, out var order);
            HasFeedbackLoop = result.HasCycle;

            // لا نحسب بيانات غير صالحة أو جزءًا من دارة تحتوي على حلقة.
            if (!result.IsValid)
                return RecordResult(result);

            var incoming = new Dictionary<IGate, List<Wire>>(ReferenceEqualityComparer.Instance);
            foreach (var gate in order)
            {
                Array.Clear(gate.Inputs, 0, gate.Inputs.Length);
                incoming.Add(gate, new List<Wire>());
            }

            foreach (var wire in Wires)
                incoming[wire.TargetGate].Add(wire);

            foreach (var gate in order)
            {
                // المصادر محسوبة بالفعل؛ والمداخل غير الموصولة تظل false.
                foreach (var wire in incoming[gate])
                    wire.TransferSignal();

                gate.Evaluate();
            }

            return RecordResult(ValidationResult.Success());
        }

        private ValidationResult RecordResult(ValidationResult result)
        {
            LastError = result.IsValid ? string.Empty : result.Message;
            return result;
        }
    }
}
