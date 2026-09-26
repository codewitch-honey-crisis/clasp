const unsigned char* s = (const unsigned char*)path_and_query;
int state = 1, accept = -1, bol = 1;
for (;; s++) {
	int at_end = *s == '\0' || *s == '?';
	if (bol && fsm_data[state + 1] != -1) state = fsm_data[state + 1];                         /* ^ */
	if ((at_end || *s == fsm_data[0]) && fsm_data[state + 2] != -1) state = fsm_data[state + 2];     /* $ */
	if (at_end) return fsm_data[state];
	int c = *s, next = -1;
	const TYPE* r = fsm_data + state + 4;
	for (int k = 0; k < fsm_data[state + 3] && c >= r[0]; k++, r += 3)
		if (c <= r[1]) { next = r[2]; break; }
	if (next == -1) break;
	state = next;
	bol = (c == fsm_data[0]);
}
return accept;
