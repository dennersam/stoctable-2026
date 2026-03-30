import { create } from 'zustand';

interface BranchState {
  branchId: string | null;
  branchName: string | null;
  setBranch: (id: string, name: string) => void;
  clearBranch: () => void;
}

export const useBranchStore = create<BranchState>((set) => ({
  branchId: localStorage.getItem('branchId'),
  branchName: localStorage.getItem('branchName'),

  setBranch: (id, name) => {
    localStorage.setItem('branchId', id);
    localStorage.setItem('branchName', name);
    set({ branchId: id, branchName: name });
  },

  clearBranch: () => {
    localStorage.removeItem('branchId');
    localStorage.removeItem('branchName');
    set({ branchId: null, branchName: null });
  },
}));
