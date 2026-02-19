using System;
using System.Collections.Generic;

namespace d2c_launcher.Models;

public sealed record PartySnapshot(IReadOnlyList<PartyMemberView> Members, DateTimeOffset? EnterQueueAt);
