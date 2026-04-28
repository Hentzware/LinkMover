// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

namespace LinkMover.Inventory.Repoint;

public enum RepointStage
{
    PreFlight,
    Copying,
    Verifying,
    RemovingOldJunction,
    CreatingNewJunction,
    DeletingOldTarget,
    Done
}
