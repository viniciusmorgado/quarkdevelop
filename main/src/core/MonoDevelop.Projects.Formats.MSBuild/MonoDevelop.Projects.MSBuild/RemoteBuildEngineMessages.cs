//
// RemoteBuildEngineMessages.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2016 Xamarin, Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.Projects.MSBuild
{
	[MessageDataTypeAttribute]
	sealed class InitializeRequest: BinaryMessage
	{
		[MessageDataProperty]
		public int IdeProcessId { get; set; }

		[MessageDataProperty]
		public string CultureName { get; set; }

		[MessageDataProperty]
		public string BinDir { get; set; }

		[MessageDataProperty]
		public Dictionary<string, string> GlobalProperties { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class LoadProjectRequest: BinaryMessage<LoadProjectResponse>
	{
		[MessageDataProperty]
		public string ProjectFile { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class LoadProjectResponse: BinaryMessage
	{
		[MessageDataProperty]
		public int ProjectId { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class UnloadProjectRequest: BinaryMessage
	{
		[MessageDataProperty]
		public int ProjectId { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class CancelTaskRequest: BinaryMessage
	{
		[MessageDataProperty]
		public int TaskId { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class SetGlobalPropertiesRequest: BinaryMessage
	{
		[MessageDataProperty]
		public Dictionary<string, string> Properties { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class PingRequest: BinaryMessage
	{
		[MessageDataProperty]
		public int TaskId = 1;
	}

	[MessageDataTypeAttribute]
	sealed class DisposeRequest: BinaryMessage
	{
	}

	[MessageDataTypeAttribute]
	sealed class RefreshProjectRequest: BinaryMessage
	{
		[MessageDataProperty]
		public int ProjectId { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class RefreshWithContentRequest: BinaryMessage
	{
		[MessageDataProperty]
		public int ProjectId { get; set; }

		[MessageDataProperty]
		public string Content { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class RunProjectRequest: BinaryMessage<RunProjectResponse>
	{
		[MessageDataProperty]
		public int ProjectId { get; set; }

		[MessageDataProperty]
		public string Content { get; set; }

		[MessageDataProperty]
		public ProjectConfigurationInfo [] Configurations { get; set; }

		[MessageDataProperty]
		public int LogWriterId { get; set; }

		[MessageDataProperty]
		public MSBuildEvent EnabledLogEvents { get; set; }

		[MessageDataProperty]
		public MSBuildVerbosity Verbosity { get; set; }

		[MessageDataProperty]
		public string [] RunTargets { get; set; }

		[MessageDataProperty]
		public string [] EvaluateItems { get; set; }

		[MessageDataProperty]
		public string [] EvaluateProperties { get; set; }

		[MessageDataProperty]
		public Dictionary<string, string> GlobalProperties { get; set; }

		[MessageDataProperty]
		public int TaskId { get; set; }

		[MessageDataProperty]
		public string BinLogFilePath { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class RunProjectResponse : BinaryMessage
	{
		[MessageDataProperty]
		public MSBuildResult Result { get; set; }
	}

	[MessageDataTypeAttribute]
	sealed class LogMessage : BinaryMessage
	{
		[MessageDataProperty]
		public int LoggerId { get; set; }

		[MessageDataProperty]
		public string LogText { get; set; }

		[MessageDataProperty]
		public LogEvent[] Events { get; set; }
	}

	[MessageDataType]
	sealed class LogEvent
	{
		[MessageDataProperty]
		public MSBuildEvent Event { get; set; }

		[MessageDataProperty]
		public string Message { get; set; }
	}

	public enum MSBuildVerbosity
	{
		Quiet,
		Minimal,
		Normal,
		Detailed,
		Diagnostic
	}

	[MessageDataTypeAttribute]
	sealed class ProjectConfigurationInfo
	{
		[MessageDataProperty]
		public string ProjectFile { get; set; }

		[MessageDataProperty]
		public string ProjectGuid { get; set; }

		[MessageDataProperty]
		public string Configuration { get; set; }

		[MessageDataProperty]
		public string Platform { get; set; }

		[MessageDataProperty]
		public bool Enabled { get; set; }
	}

	[MessageDataType]
	sealed class LoggerInfo
	{
		[MessageDataProperty]
		public string Id { get; set; }

		[MessageDataProperty]
		public bool ConsoleLog { get; set; }

		[MessageDataProperty]
		public MSBuildEvent EventsFilter { get; set; }
	}

	[MessageDataType]
	sealed class BeginBuildRequest : BinaryMessage
	{
		[MessageDataProperty]
		public string BinLogFilePath { get; set; }

		[MessageDataProperty]
		public int LogWriterId { get; set; }

		[MessageDataProperty]
		public MSBuildEvent EnabledLogEvents { get; set; }

		[MessageDataProperty]
		public MSBuildVerbosity Verbosity { get; set; }

		[MessageDataProperty]
		public ProjectConfigurationInfo [] Configurations { get; set; }
	}

	[MessageDataType]
	sealed class EndBuildRequest : BinaryMessage
	{
	}
}

