import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor, cleanup } from "@testing-library/react";
import type { WorkOrderDetailDto } from "@/lib/api/types";

/**
 * The work-order detail page is where SecurityManager and Technician actions
 * are gated client-side (assign/finish-resolution vs. start/note/complete).
 * These are UX regression smoke tests — backend authorization remains
 * authoritative regardless of what this page renders.
 */

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: "wo-1" }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...rest}>{children}</a>
  ),
}));

const mockUseAuth = vi.fn();
vi.mock("@/lib/auth/AuthContext", () => ({
  useAuth: () => mockUseAuth(),
}));

const mockGetWorkOrderById = vi.fn();
vi.mock("@/lib/api/client", () => ({
  getWorkOrderById: (...args: unknown[]) => mockGetWorkOrderById(...args),
  assignTechnician: vi.fn(),
  startWork: vi.fn(),
  addTechnicianNote: vi.fn(),
  completeWorkOrder: vi.fn(),
  getTechnicians: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 }),
  updateAssetStatus: vi.fn(),
  updateIncidentStatus: vi.fn(),
}));

function baseWorkOrder(overrides: Partial<WorkOrderDetailDto>): WorkOrderDetailDto {
  return {
    id: "wo-1",
    securityAssetId: "asset-1",
    securityIncidentId: "incident-1",
    title: "Pharmacy Storage Camera Repair",
    description: "Camera offline, needs replacement.",
    priority: "Critical",
    status: "New",
    assetName: "Pharmacy Storage Camera 02",
    locationName: "Pharmacy Storage",
    assignedTechnician: null,
    assignedAt: null,
    startedAt: null,
    completedAt: null,
    completionSummary: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    notes: [],
    ...overrides,
  };
}

function mockAuthAs(roles: string[]) {
  mockUseAuth.mockReturnValue({
    user: { sub: "test-user", name: "Test User", email: "", roles },
    isAuthenticated: true,
    isLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
    hasRole: (role: string) => roles.includes(role),
    hasAnyRole: (...check: string[]) => check.some((r) => roles.includes(r)),
  });
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("WorkOrderDetailPage role-aware actions", () => {
  it("shows SecurityManager the Assign Technician action on a New work order", async () => {
    mockAuthAs(["SecurityManager"]);
    mockGetWorkOrderById.mockResolvedValue(baseWorkOrder({ status: "New" }));

    const { default: WorkOrderDetailPage } = await import("./page");
    render(<WorkOrderDetailPage />);

    await waitFor(() => expect(screen.getByText("Pharmacy Storage Camera Repair")).toBeInTheDocument());

    expect(screen.getByRole("button", { name: "Assign Technician" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Start Work" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add Repair Note" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Complete Work" })).not.toBeInTheDocument();
  });

  it("shows SecurityManager the Finish Security Resolution action on a Completed work order", async () => {
    mockAuthAs(["SecurityManager"]);
    mockGetWorkOrderById.mockResolvedValue(baseWorkOrder({ status: "Completed" }));

    const { default: WorkOrderDetailPage } = await import("./page");
    render(<WorkOrderDetailPage />);

    await waitFor(() => expect(screen.getByText("Pharmacy Storage Camera Repair")).toBeInTheDocument());

    expect(screen.getByRole("button", { name: "Finish Security Resolution" })).toBeInTheDocument();
  });

  it("does not show Technician-only repair actions to SecurityManager on an InProgress work order", async () => {
    mockAuthAs(["SecurityManager"]);
    mockGetWorkOrderById.mockResolvedValue(baseWorkOrder({ status: "InProgress" }));

    const { default: WorkOrderDetailPage } = await import("./page");
    render(<WorkOrderDetailPage />);

    await waitFor(() => expect(screen.getByText("Pharmacy Storage Camera Repair")).toBeInTheDocument());

    expect(screen.queryByRole("button", { name: "Add Repair Note" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Complete Work" })).not.toBeInTheDocument();
  });

  it("shows Technician the Start Work action on an Assigned work order", async () => {
    mockAuthAs(["Technician"]);
    mockGetWorkOrderById.mockResolvedValue(baseWorkOrder({ status: "Assigned" }));

    const { default: WorkOrderDetailPage } = await import("./page");
    render(<WorkOrderDetailPage />);

    await waitFor(() => expect(screen.getByText("Pharmacy Storage Camera Repair")).toBeInTheDocument());

    expect(screen.getByRole("button", { name: "Start Work" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Assign Technician" })).not.toBeInTheDocument();
  });

  it("shows Technician Add Repair Note and Complete Work actions on an InProgress work order", async () => {
    mockAuthAs(["Technician"]);
    mockGetWorkOrderById.mockResolvedValue(baseWorkOrder({ status: "InProgress" }));

    const { default: WorkOrderDetailPage } = await import("./page");
    render(<WorkOrderDetailPage />);

    await waitFor(() => expect(screen.getByText("Pharmacy Storage Camera Repair")).toBeInTheDocument());

    expect(screen.getByRole("button", { name: "Add Repair Note" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Complete Work" })).toBeInTheDocument();
  });

  it("does not show SecurityManager supervisory actions to Technician", async () => {
    mockAuthAs(["Technician"]);
    mockGetWorkOrderById.mockResolvedValue(baseWorkOrder({ status: "New" }));

    const { default: WorkOrderDetailPage } = await import("./page");
    render(<WorkOrderDetailPage />);

    await waitFor(() => expect(screen.getByText("Pharmacy Storage Camera Repair")).toBeInTheDocument());

    expect(screen.queryByRole("button", { name: "Assign Technician" })).not.toBeInTheDocument();
  });
});
