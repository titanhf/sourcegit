using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CleanupLocalBranchItem : ObservableObject
    {
        public Models.Branch Branch
        {
            get;
        }

        public bool IsUpstreamGone
        {
            get;
        }

        public bool IsMerged
        {
            get;
        }

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }

        public CleanupLocalBranchItem(Models.Branch branch, bool isUpstreamGone, bool isMerged)
        {
            Branch = branch;
            IsUpstreamGone = isUpstreamGone;
            IsMerged = isMerged;
            _isChecked = isMerged;
        }

        private bool _isChecked = false;
    }

    public class CleanupLocalBranches : Popup
    {
        public List<CleanupLocalBranchItem> Candidates
        {
            get => _candidates;
            private set => SetProperty(ref _candidates, value);
        }

        public bool HasCandidates
        {
            get => _hasCandidates;
            private set => SetProperty(ref _hasCandidates, value);
        }

        public bool IncludeUnpushed
        {
            get => _includeUnpushed;
            set
            {
                if (SetProperty(ref _includeUnpushed, value))
                    UpdateCandidates();
            }
        }

        public CleanupLocalBranches(Repository repo, HashSet<string> merged)
        {
            _repo = repo;

            foreach (var b in repo.Branches)
            {
                // Skip HEAD and branches checked out by any worktree.
                if (!b.IsLocal || b.IsCurrent || b.IsDetachedHead || !string.IsNullOrEmpty(b.WorktreePath))
                    continue;

                var isMerged = merged.Contains(b.FullName);
                if (string.IsNullOrEmpty(b.Upstream))
                {
                    if (isMerged)
                        _unpushed.Add(new CleanupLocalBranchItem(b, false, true));
                }
                else if (b.IsUpstreamGone && b.Upstream.StartsWith("refs/remotes/", StringComparison.Ordinal))
                {
                    _gone.Add(new CleanupLocalBranchItem(b, true, isMerged));
                }
            }

            UpdateCandidates();
        }

        public void SelectAll()
        {
            foreach (var item in _candidates)
                item.IsChecked = true;
        }

        public void UnselectAll()
        {
            foreach (var item in _candidates)
                item.IsChecked = false;
        }

        public override async Task<bool> Sure()
        {
            var targets = _candidates.FindAll(x => x.IsChecked);
            if (targets.Count == 0)
                return true;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Cleaning up local branches...";

            var log = _repo.CreateLog("Cleanup Local Branches");
            Use(log);

            // Failures are recorded in log and do not stop the remaining deletions.
            foreach (var target in targets)
                await new Commands.Branch(_repo.FullPath, target.Branch.Name)
                    .Use(log)
                    .DeleteLocalAsync(!target.IsMerged);

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private void UpdateCandidates()
        {
            var candidates = new List<CleanupLocalBranchItem>();
            candidates.AddRange(_gone);
            if (_includeUnpushed)
                candidates.AddRange(_unpushed);

            Candidates = candidates;
            HasCandidates = candidates.Count > 0;
        }

        private readonly Repository _repo = null;
        private readonly List<CleanupLocalBranchItem> _gone = [];
        private readonly List<CleanupLocalBranchItem> _unpushed = [];
        private List<CleanupLocalBranchItem> _candidates = [];
        private bool _hasCandidates = false;
        private bool _includeUnpushed = false;
    }
}
