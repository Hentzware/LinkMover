// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

namespace LinkMover.Core.Models;

public sealed record PreFlightReport(IReadOnlyList<PreFlightCheck> Checks)
{
    public bool IsBlocking => Checks.Any(c => c.Severity == Severity.Error);
}
