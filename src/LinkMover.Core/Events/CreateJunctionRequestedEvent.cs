// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;
using Prism.Events;

namespace LinkMover.Core.Events;

// Published by the analyzer (or any other module) to ask the wizard
// to start with a pre-filled JunctionPlan.
public sealed class CreateJunctionRequestedEvent : PubSubEvent<JunctionPlan>;
