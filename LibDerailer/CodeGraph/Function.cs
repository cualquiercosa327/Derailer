﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gee.External.Capstone.Arm;
using LibDerailer.CodeGraph.Nodes;

namespace LibDerailer.CodeGraph
{
    public class Function
    {
        public uint   Address { get; }
        public string Name    { get; }

        public List<BasicBlock> BasicBlocks { get; } = new List<BasicBlock>();
        public BasicBlock Epilogue { get; set; }

        public Variable[] MachineRegisterVariables { get; }

        public int            StackOffset    { get; }
        public List<Variable> StackVariables { get; } = new List<Variable>();

        public Function(uint address, int stackOffset = 0)
        {
            Address     = address;
            StackOffset = stackOffset;

            MachineRegisterVariables = new Variable[17];
            for (int i = 0; i < 16; i++)
                MachineRegisterVariables[i] = new Variable(VariableLocation.Register, $"r{i}", i, 4);
            MachineRegisterVariables[16] = new Variable(VariableLocation.Register, "cpsr", 16, 4);
        }

        public string BasicBlockGraphToDot()
        {
            int id        = 0;
            var uniqueIds = new Dictionary<Instruction, int>();
            var sb        = new StringBuilder();
            sb.AppendLine("digraph func {");
            foreach (var block in BasicBlocks)
            {
                foreach (var successor in block.Successors)
                {
                    ArmConditionCode cond = ArmConditionCode.ARM_CC_AL;
                    BasicBlock otherSucc;
                    if (successor.BlockConditionInstruction != null)
                        cond = successor.BlockCondition;
                    else if(block.Successors.Count == 2 && (otherSucc = block.Successors.First(s => s != successor)).BlockConditionInstruction != null)
                        cond = ArmUtil.GetOppositeCondition(otherSucc.BlockCondition);
                    else if (block.Instructions.Last() is Branch b)
                    {
                        if(successor == b.Destination)
                            cond = b.Condition == block.BlockCondition ? ArmConditionCode.ARM_CC_AL : b.Condition;
                        else if (block.Successors.Count == 2)
                            cond = ArmUtil.GetOppositeCondition(b.Condition);
                    }

                    sb.AppendLine($"\"0x{block.Address:X08}\" -> \"0x{successor.Address:X08}\" [label=\"{cond}\"];");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        public string DefUseGraphToDot()
        {
            int id        = 0;
            var uniqueIds = new Dictionary<Instruction, int>();
            var sb        = new StringBuilder();
            sb.AppendLine("digraph func {");
            foreach (var block in BasicBlocks)
            {
                foreach (var inst in block.Instructions)
                {
                    if (!uniqueIds.ContainsKey(inst))
                        uniqueIds[inst] = id++;
                    foreach (var use in inst.VariableUses)
                    {
                        foreach (var useLoc in inst.VariableUseLocs[use])
                        {
                            if (!uniqueIds.ContainsKey(useLoc))
                                uniqueIds[useLoc] = id++;
                            sb.AppendLine(
                                $"\"{uniqueIds[useLoc]}: {useLoc}\" -> \"{uniqueIds[inst]}: {inst}\" [label=\"{use}\"];");
                        }
                    }
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}