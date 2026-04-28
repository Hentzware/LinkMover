// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;
using Prism.Mvvm;

namespace LinkMover.JunctionCreation.Wizard;

public sealed class WizardContext : BindableBase
{
    private string _source = string.Empty;
    private string _target = string.Empty;
    private bool _dryRun;
    private long _sourceSizeBytes;
    private PreFlightReport? _preFlight;
    private RobocopyResult? _copyResult;
    private VerifyResult? _verifyResult;
    private bool _cleanupDone;
    private bool _linkCreated;
    private JunctionInfo? _junctionInfo;
    private IReadOnlyList<LockingProcessInfo> _lockingProcesses = Array.Empty<LockingProcessInfo>();

    public string Source { get => _source; set => SetProperty(ref _source, value); }
    public string Target { get => _target; set => SetProperty(ref _target, value); }
    public bool DryRun { get => _dryRun; set => SetProperty(ref _dryRun, value); }
    public long SourceSizeBytes { get => _sourceSizeBytes; set => SetProperty(ref _sourceSizeBytes, value); }
    public PreFlightReport? PreFlight { get => _preFlight; set => SetProperty(ref _preFlight, value); }
    public RobocopyResult? CopyResult { get => _copyResult; set => SetProperty(ref _copyResult, value); }
    public VerifyResult? VerifyResult { get => _verifyResult; set => SetProperty(ref _verifyResult, value); }
    public bool CleanupDone { get => _cleanupDone; set => SetProperty(ref _cleanupDone, value); }
    public bool LinkCreated { get => _linkCreated; set => SetProperty(ref _linkCreated, value); }
    public JunctionInfo? JunctionInfo { get => _junctionInfo; set => SetProperty(ref _junctionInfo, value); }
    public IReadOnlyList<LockingProcessInfo> LockingProcesses { get => _lockingProcesses; set => SetProperty(ref _lockingProcesses, value); }

    public void Reset()
    {
        Source = string.Empty;
        Target = string.Empty;
        DryRun = false;
        SourceSizeBytes = 0;
        PreFlight = null;
        CopyResult = null;
        VerifyResult = null;
        CleanupDone = false;
        LinkCreated = false;
        JunctionInfo = null;
        LockingProcesses = Array.Empty<LockingProcessInfo>();
    }
}
