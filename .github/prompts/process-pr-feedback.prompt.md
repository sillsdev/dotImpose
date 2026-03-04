---
description: read and process comments in a pr. Only appropriate to use in the rare situations where developer pre-review isn't needed.
---

Use the gh tool to determine the PR associated with the current branch. If you cannot find one, use the askQuestions tool to ask the user for a url.
Read the unresolved pr comments and either answer them or handle the problem they call out and then answer them.
When you answer, prefix your response with the name of your model, e.g. [hall9000]. I may ask you to "cycle" in which case you should make fixes and stop, or if you should also make a commit and push it and then wait 10 minutes for new comments and cycle. If I don't ask you to cycle, use the askQuestions tool to ask me if I should cycle or not.
