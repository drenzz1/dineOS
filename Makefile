# =============================================================================
# dineOS — top-level Makefile
# Provides developer-ergonomic shortcuts for the Helm chart verification
# workflow. All targets are POSIX-compatible and run on Linux / macOS CI.
# Windows users: install make via Chocolatey (`choco install make`) or run
# the underlying commands directly.
# =============================================================================

CHART         := deploy/helm/dineos
VALUES_LOCAL  := deploy/helm/dineos/values.local.yaml
RELEASE       := dineos
NAMESPACE     := dineos

.PHONY: help helm-lint helm-dry-run helm-verify helm-smoke

# Default target — print available targets
help:
	@echo ""
	@echo "dineOS Makefile targets"
	@echo "─────────────────────────────────────────────────────"
	@echo "  helm-lint      Lint the Helm chart (no cluster needed)"
	@echo "  helm-dry-run   Render manifests and validate with kubeconform (no cluster needed)"
	@echo "  helm-verify    Run helm-lint + helm-dry-run (CI gate, no cluster needed)"
	@echo "  helm-smoke     Full kind smoke test: install → rollout → HTTP check → teardown"
	@echo "─────────────────────────────────────────────────────"
	@echo ""

# -----------------------------------------------------------------------------
# helm-lint — chart structure + template syntax check
# -----------------------------------------------------------------------------
helm-lint:
	@echo "→ helm lint $(CHART)"
	helm lint $(CHART) --values $(VALUES_LOCAL)

# -----------------------------------------------------------------------------
# helm-dry-run — render every manifest and validate with kubeconform.
# No live cluster required. kubectl --dry-run=client is intentionally NOT used:
# kubectl 1.33+ contacts the API server unconditionally even with --validate=false.
# Install kubeconform: https://github.com/yannh/kubeconform/releases
# -----------------------------------------------------------------------------
helm-dry-run:
	@echo "→ helm template | kubeconform"
	helm template $(RELEASE) $(CHART) \
		--values $(VALUES_LOCAL) \
	| kubeconform \
		-strict \
		-summary \
		-kubernetes-version 1.31.0

# -----------------------------------------------------------------------------
# helm-verify — fast pre-merge gate (lint + dry-run, no cluster)
# -----------------------------------------------------------------------------
helm-verify: helm-lint helm-dry-run
	@echo ""
	@echo "✓ helm-verify passed. Run 'make helm-smoke' for the full kind smoke test."

# -----------------------------------------------------------------------------
# helm-smoke — full end-to-end kind smoke test
# Requires: kind, docker, helm 3.14+, kubectl
# The kind cluster is deleted on exit regardless of success or failure.
# -----------------------------------------------------------------------------
helm-smoke:
	@echo "→ Running kind smoke test via scripts/devops/verify-helm.sh"
	bash scripts/devops/verify-helm.sh
