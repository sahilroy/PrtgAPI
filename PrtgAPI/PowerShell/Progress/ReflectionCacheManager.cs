﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;
using Microsoft.PowerShell.Commands;
using PrtgAPI.Helpers;
using PrtgAPI.PowerShell.Base;

namespace PrtgAPI.PowerShell.Progress
{
    [ExcludeFromCodeCoverage]
    class ReflectionCacheManager
    {
        private PSCmdlet cmdlet;

        #region Reflection Members

        private Lazy<object> runtimeOutputPipe;
        private Lazy<object> runtimeOutputPipeDownstreamCmdletProcessor;
        private Lazy<CommandInfo> runtimeOutputPipeDownstreamCmdletCommandInfo;
        private Lazy<CommandInfo> upstreamCmdletCommandInfo;
        private Lazy<object> runtimeOutputPipeDownstreamCmdletCommand;

        private Lazy<object> runtimePipelineProcessor;
        private Lazy<List<object>> runtimePipelineProcessorCommandProcessors;
        private Lazy<List<object>> runtimePipelineProcessorCommandProcessorCommands;

        #endregion
        #region Method Results

        private Lazy<object> upstreamCmdlet;
        private Lazy<PrtgCmdlet> nextPrtgCmdlet;
        private Lazy<PrtgCmdlet> previousPrtgCmdlet;

        #endregion

        public ReflectionCacheManager(PSCmdlet cmdlet)
        {
            this.cmdlet = cmdlet;

            runtimeOutputPipe                            = new Lazy<object>      (() => cmdlet.CommandRuntime.GetInternalProperty("OutputPipe"));
            runtimeOutputPipeDownstreamCmdletProcessor   = new Lazy<object>      (() => runtimeOutputPipe.Value.GetInternalProperty("DownstreamCmdlet"));
            runtimeOutputPipeDownstreamCmdletCommandInfo = new Lazy<CommandInfo> (() => (CommandInfo)runtimeOutputPipeDownstreamCmdletProcessor.Value?.GetInternalProperty("CommandInfo"));
            runtimeOutputPipeDownstreamCmdletCommand     = new Lazy<object>      (() => runtimeOutputPipeDownstreamCmdletProcessor.Value?.GetInternalProperty("Command"));

            runtimePipelineProcessor                     = new Lazy<object>      (() => cmdlet.CommandRuntime.GetInternalProperty("PipelineProcessor"));
            runtimePipelineProcessorCommandProcessors    = new Lazy<List<object>>(() =>
            {
                var commands = runtimePipelineProcessor.Value.GetInternalProperty("Commands");

                return ((IEnumerable)commands).Cast<object>().ToList();
            });
            runtimePipelineProcessorCommandProcessorCommands = new Lazy<List<object>>(() => runtimePipelineProcessorCommandProcessors.Value.Select(c => c.GetInternalProperty("Command")).ToList());

            upstreamCmdlet = new Lazy<object>(GetUpstreamCmdletInternal);
            nextPrtgCmdlet = new Lazy<PrtgCmdlet>(GetNextPrtgCmdletInternal);
            previousPrtgCmdlet = new Lazy<PrtgCmdlet>(GetPreviousPrtgCmdletInternal);

            upstreamCmdletCommandInfo = new Lazy<CommandInfo>(() => (CommandInfo)GetUpstreamCmdlet()?.GetInternalProperty("CommandInfo"));
        }

        #region Get Cmdlet From Position

        public object GetFirstCmdletInPipeline() => GetPipelineCommands().First();

        public object GetDownstreamCmdlet() => runtimeOutputPipeDownstreamCmdletCommand.Value;

        private object GetUpstreamCmdletInternal()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            if (myIndex <= 0)
                return null;

            var previousIndex = myIndex - 1;

            var previousCmdlet = commands[previousIndex];

            return previousCmdlet;
        }

        public object GetUpstreamCmdlet() => upstreamCmdlet.Value;

        private PrtgCmdlet GetNextPrtgCmdletInternal()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            for (int i = myIndex + 1; i < commands.Count; i++)
            {
                if (commands[i] is PrtgCmdlet)
                    return (PrtgCmdlet) commands[i];
            }

            return null;
        }

        public PrtgCmdlet GetNextPrtgCmdlet() => nextPrtgCmdlet.Value;

        /// <summary>
        /// Returns the previous PrtgCmdlet before this one. If no previous cmdlet was a PrtgCmdlet, this method returns null.
        /// </summary>
        /// <returns>If a previous cmdlet is a PrtgCmdlet, that cmdlet. Otherwise, null.</returns>
        private PrtgCmdlet GetPreviousPrtgCmdletInternal()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            for (int i = myIndex - 1; i >= 0; i--)
            {
                if (commands[i] is PrtgCmdlet)
                    return (PrtgCmdlet) commands[i];
            }

            return null;
        }

        public PrtgCmdlet GetPreviousPrtgCmdlet() => previousPrtgCmdlet.Value;

        public PrtgCmdlet TryGetPreviousPrtgCmdletOfNotType<T>()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            for (int i = myIndex - 1; i >= 0; i--)
            {
                if (commands[i] is PrtgCmdlet)
                {
                    if (!(commands[i] is T) || i == 0)
                    {
                        return (PrtgCmdlet)commands[i];
                    }
                }
            }

            return null;
        }

        #endregion
        #region Pipeline Has Cmdlet

        /// <summary>
        /// Indicates whether the current pipeline contains a cmdlet of a specified type
        /// </summary>
        /// <typeparam name="T">The type of cmdlet to check for.</typeparam>
        /// <returns>True if the pipeline history contains a cmdlet of the specified type. Otherwise, false.</returns>
        public bool PipelineHasCmdlet<T>() where T : Cmdlet
        {
            if (cmdlet is T)
                return true;

            var commands = GetPipelineCommands();

            return commands.Any(c => c is T);
        }

        public bool PipelineSoFarHasCmdlet<T>() where T : Cmdlet
        {
            if (cmdlet is T)
                return true;

            return PipelineBeforeMeHasCmdlet<T>();
        }

        public bool PipelineBeforeMeHasCmdlet<T>() where T : Cmdlet
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            return commands.Take(myIndex).Any(c => c is T);
        }

        public bool PipelineAfterMeIsCmdlet<T>() where T : Cmdlet
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet) + 1;

            return commands.Skip(myIndex).FirstOrDefault(c => c is T) != null;
        }

        public bool PipelineRemainingHasCmdlet<T>() where T : Cmdlet
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            return commands.Skip(myIndex + 1).Any(c => c is T);
        }

        #endregion
        #region Pipeline Purity

        //whats the difference between this and PipelineIsProgressPureFromPrtgCmdlet

        public bool PipelineIsProgressPure()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            if (myIndex <= 0)
                return true;

            for (int i = 0; i < myIndex; i++)
            {
                var command = commands[i];

                if (!(command is PrtgCmdlet || ProgressManager.IsPureThirdPartyCmdlet(command.GetType())))
                    return false;
            }

            return true;
        }

        public bool PipelineIsProgressPureFromLastPrtgCmdlet()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            for (var i = myIndex - 1; i >= 0; i--)
            {
                if (commands[i] is PrtgCmdlet)
                    return true;

                if (!ProgressManager.IsPureThirdPartyCmdlet(commands[i].GetType()))
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Indicates whether the current pipeline contains progress compatible cmdlets all the way to the next <see cref="PrtgCmdlet"/>. Returns false if there are no more <see cref="PrtgCmdlet"/> objects in the pipeline.
        /// </summary>
        /// <returns></returns>
        public bool PipelineIsProgressPureToNextPrtgCmdlet()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            for (var i = myIndex + 1; i < commands.Count; i++)
            {
                if (commands[i] is PrtgCmdlet)
                    return true;

                if (!ProgressManager.IsPureThirdPartyCmdlet(commands[i].GetType()))
                    return false;
            }

            return false;
        }

        public bool PipelineContainsBlockingCmdletToNextPrtgCmdletOrEnd()
        {
            var commands = GetPipelineCommands();

            var myIndex = commands.IndexOf(cmdlet);

            for (var i = myIndex + 1; i < commands.Count; i++)
            {
                if (commands[i] is PrtgCmdlet)
                    return false;

                if (commands[i] is SelectObjectCommand)
                {
                    var selectObject = (SelectObjectCommand) commands[i];
                    var boundParameters = selectObject.MyInvocation.BoundParameters;

                    if (boundParameters.ContainsKey("Last"))
                        return true;

                    if (boundParameters.ContainsKey("SkipLast"))
                        return true;
                }
            }

            return false;
        }

        #endregion
        #region Pipeline Input

        /// <summary>
        /// Retrieve the input to the entire pipeline.
        /// </summary>
        /// <returns></returns>
        public Pipeline GetPipelineInput()
        {
            var processor = runtimePipelineProcessorCommandProcessors.Value.First();

            var command = (InternalCommand) runtimePipelineProcessorCommandProcessorCommands.Value.First();

            var runtime = (ICommandRuntime)processor.GetInternalProperty("CommandRuntime");

            return GetCmdletPipelineInput(runtime, command);
        }

        /// <summary>
        /// Retrieve the input to the current cmdlet.
        /// </summary>
        /// <returns></returns>
        public Pipeline GetCmdletPipelineInput()
        {
            return GetCmdletPipelineInput(cmdlet.CommandRuntime, cmdlet);
        }

        public Pipeline GetSelectPipelineOutput()
        {
            var command = (SelectObjectCommand)GetUpstreamCmdlet();

            var queue = (Queue<PSObject>) command.GetInternalField("selectObjectQueue");

            var cmdletPipeline = GetCmdletPipelineInput();

            cmdletPipeline.List.AddRange(queue.Cast<object>().ToList());

            var list = cmdletPipeline.List;

            if (command.MyInvocation.BoundParameters.ContainsKey("SkipLast"))
            {
                list = cmdletPipeline.List.Take(cmdletPipeline.List.Count - 1).ToList();
            }

            return new Pipeline(cmdletPipeline.List.First(), list);
        }

        /// <summary>
        /// Retrieve the pipeline input of a specified cmdlet.
        /// </summary>
        /// <param name="commandRuntime">The runtime of the cmdlet whose pipeline should be retrieved.</param>
        /// <param name="cmdlet">The cmdlet whose pipeline should be retrieved.</param>
        /// <returns></returns>
        private static Pipeline GetCmdletPipelineInput(ICommandRuntime commandRuntime, InternalCommand cmdlet)
        {
            var inputPipe = commandRuntime.GetInternalProperty("InputPipe");
            var enumerator = inputPipe.GetInternalField("_enumeratorToProcess");

            var currentPS = (PSObject)cmdlet.GetInternalProperty("CurrentPipelineObject");

            var current = currentPS?.ToString() == string.Empty || currentPS == null ? null : currentPS.BaseObject;

            if (enumerator == null) //Piping from a cmdlet
            {
                if (current == null)
                    return null;

                return new Pipeline(current, new List<object> { current });
            }
            else //Piping from a variable
            {
                var array = ((object[])enumerator.GetInternalField("_array")).Select(o =>
                {
                    if (o is PSObject)
                        return o;
                    else
                        return new PSObject(o);
                }).Cast<PSObject>();

                return new Pipeline(current, array.Select(e => e.BaseObject).ToList());
            }
        }

        #endregion

        /// <summary>
        /// Retrieves the <see cref="CommandProcessor"/> of the next cmdlet.
        /// </summary>
        /// <returns></returns>
        public object GetDownstreamCmdletProcessor() => runtimeOutputPipeDownstreamCmdletProcessor.Value;

        public CommandInfo GetDownstreamCmdletInfo() => runtimeOutputPipeDownstreamCmdletCommandInfo.Value;

        public CommandInfo GetUpstreamCmdletInfo() => upstreamCmdletCommandInfo.Value;

        public List<object> GetPipelineCommands() => runtimePipelineProcessorCommandProcessorCommands.Value;
    }
}
