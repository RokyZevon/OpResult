namespace OpResult.Analyzers.Tests;

using Xunit;

public sealed class OpResultUsageAnalyzerTests
{
    [Fact]
    public async Task EmptyMethod_DoesNotReportDiagnostics()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var user = new User(1);
            _ = user;
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnguardedValueAccess_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            _ = result.Value;
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task UnguardedValueAccess_WithValueTypePayload_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadNumber(found: false);
            _ = result.Value;
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task UnguardedErrorAccess_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            _ = result.Error;
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task ValueAccess_InsideIsOkBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                _ = result.Value;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_InsideNegatedIsErrBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (!result.IsErr)
            {
                _ = result.Value;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_InsideCompoundIsOkBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var user = new User(1);
            var result = LoadUser(found: true);
            if (result.IsOk && user.Id > 0)
            {
                _ = result.Value;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_InsidePartialOrBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var user = new User(1);
            var result = LoadUser(found: true);
            if (result.IsOk || user.Id > 0)
            {
                _ = result.Value;
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_AfterReassignmentInsideIsOkBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                result = LoadUser(found: false);
                _ = result.Value;
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_AfterOutArgumentMutationInsideIsOkBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                Replace(out result);
                _ = result.Value;
            }

            void Replace(out global::OpResult.OpResult<User> target)
            {
                target = LoadUser(found: false);
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_AfterRefArgumentMutationInsideIsOkBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                Replace(ref result);
                _ = result.Value;
            }

            void Replace(ref global::OpResult.OpResult<User> target)
            {
                target = LoadUser(found: false);
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_AfterDeconstructionAssignmentInsideIsOkBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                (result, _) = (LoadUser(found: false), 0);
                _ = result.Value;
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_InsideLambdaWithinIsOkBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                global::System.Action action = () => _ = result.Value;
                action();
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ValueAccess_InsideLocalFunctionWithinIsOkBranch_ReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                void ReadValue() => _ = result.Value;
                ReadValue();
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ErrorAccess_InsideIsErrBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                _ = result.Error;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task ErrorAccess_InsideNegatedIsOkBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (!result.IsOk)
            {
                _ = result.Error;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task ValueAccess_AfterIsErrEarlyReturn_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsErr)
            {
                return;
            }

            _ = result.Value;
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ErrorAccess_AfterIsOkEarlyReturn_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsOk)
            {
                return;
            }

            _ = result.Error;
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task ValueAndErrorAccess_InSplitBranches_DoesNotReportDiagnostics()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            if (result.IsOk)
            {
                _ = result.Value;
            }
            else
            {
                _ = result.Error;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task ValueAccess_InsideFieldIsOkBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            """
            #nullable enable

            using global::OpResult;

            public sealed class User
            {
                public User(int id) => Id = id;
                public int Id { get; }
            }

            public sealed class Probe
            {
                private OpResult<User> _cached = OpResults.Ok(new User(1));

                public void Run()
                {
                    if (_cached.IsOk)
                    {
                        _ = _cached.Value;
                    }
                }
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task ErrorAccess_InsidePropertyIsErrBranch_DoesNotReportDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            """
            #nullable enable

            using global::OpResult;

            public sealed class User
            {
                public User(int id) => Id = id;
                public int Id { get; }
            }

            public sealed class Probe
            {
                public OpResult<User> Cached { get; set; } = OpResults.Err<User>("not found");

                public void Run()
                {
                    if (Cached.IsErr)
                    {
                        _ = Cached.Error;
                    }
                }
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task OnOk_DoesNotProveSubsequentValueAccess()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: true);
            result.OnOk(user => _ = user.Id);
            _ = result.Value;
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task UnguardedValueAccess_WithNullableDisabled_StillReportsDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            _ = result.Value;
            """,
            nullableEnabled: false);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
    }

    [Fact]
    public async Task NameofValueAndError_DoesNotReportUnguardedAccessDiagnostics()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            _ = nameof(result.Value);
            _ = nameof(result.Error);
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
    }

    [Fact]
    public async Task ValueNotNullCheck_ReportsPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.Value != null)
            {
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ValueNullCheck_ReportsPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.Value == null)
            {
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ErrorNotNullCheck_ReportsPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.Error != null)
            {
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ErrorNullCheck_ReportsPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.Error == null)
            {
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ErrorMessageEmptyLiteralCheck_ReportsPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.Error.Message == "")
            {
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ErrorMessageStringEmptyCheck_ReportsPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.Error.Message == string.Empty)
            {
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ReversedNullChecks_ReportPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (null != result.Value)
            {
            }

            if (null == result.Error)
            {
            }
            """);

        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == DiagnosticIds.PseudoBranchTest));
    }

    [Fact]
    public async Task ReversedErrorMessageEmptyChecks_ReportPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if ("" == result.Error.Message)
            {
            }

            if (string.Empty == result.Error.Message)
            {
            }
            """);

        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == DiagnosticIds.PseudoBranchTest));
    }

    [Fact]
    public async Task ErrorMessageIsNullOrEmptyOnLocalInsideGuard_DoesNotReportPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                var message = result.Error.Message;
                _ = string.IsNullOrEmpty(message);
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task ErrorMessageLengthZeroInsideGuard_DoesNotReportPseudoBranchDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                _ = result.Error.Message.Length == 0;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.PseudoBranchTest);
    }

    [Fact]
    public async Task BareOpResultCall_ReportsUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            SaveUser(new User(1));
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task BareOpResultFluentCall_ReportsUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            LoadUser(found: true).OnOk(user => _ = user.Id);
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task AssignedOpResultThenDiscarded_DoesNotReportUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = SaveUser(new User(1));
            _ = result;
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task DiscardAssignedOpResultCall_DoesNotReportUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            _ = SaveUser(new User(1));
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task OpResultCallUsedInCondition_DoesNotReportUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            if (SaveUser(new User(1)).IsErr)
            {
                return;
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task ReturnedOpResultCall_DoesNotReportUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            global::OpResult.OpResult Execute()
            {
                return SaveUser(new User(1));
            }

            _ = Execute();
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task VoidMatchCall_DoesNotReportUnusedResultDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            SaveUser(new User(1)).Match(onOk: () => { }, onErr: error => _ = error.Message);
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.UnusedResultReturnValue);
    }

    [Fact]
    public async Task DirectErrorMessageRebuild_ReportsChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                _ = OpResults.Err(result.Error.Message);
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }

    [Fact]
    public async Task GenericDirectErrorMessageRebuild_ReportsChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                OpResult<User> wrapped = OpResults.Err<User>(result.Error.Message);
                _ = wrapped;
            }
            """);

        AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }

    [Fact]
    public async Task ErrorToErrInsideErrBranch_DoesNotReportChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                _ = result.Error.ToErr("Could not load profile.");
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }

    [Fact]
    public async Task OpResultsErrWithInnerError_DoesNotReportChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                _ = OpResults.Err("Could not load profile.", result.Error);
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }

    [Fact]
    public async Task LocalErrorMessageRebuild_DoesNotReportChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                var message = result.Error.Message;
                _ = OpResults.Err(message);
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }

    [Fact]
    public async Task InterpolatedErrorMessageRebuild_DoesNotReportChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            if (result.IsErr)
            {
                _ = OpResults.Err($"Failed: {result.Error.Message}");
            }
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }

    [Fact]
    public async Task DirectErrorMessageRebuildWithoutErrProof_DoesNotReportChainLossDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var result = LoadUser(found: false);
            _ = OpResults.Err(result.Error.Message);
            """);

        AnalyzerTestHost.AssertNoDiagnostic(diagnostics, DiagnosticIds.DirectErrorChainLoss);
    }
}
