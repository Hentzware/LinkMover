// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;

namespace LinkMover.JunctionCreation.Wizard;

public interface IJunctionWizardOrchestrator
{
    WizardContext Context { get; }

    void Reset();

    Task RunPreFlightAsync(CancellationToken ct);

    Task RunCopyAsync(IProgress<RobocopyProgress>? progress, CancellationToken ct);

    Task RunVerifyAsync(CancellationToken ct);

    Task RunCleanupAsync(CancellationToken ct);

    Task RunCreateLinkAsync(CancellationToken ct);
}
