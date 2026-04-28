// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using Prism.Events;

namespace LinkMover.Core.Events;

// Payload is the absolute source path of the removed junction.
public sealed class JunctionRemovedEvent : PubSubEvent<string>;
