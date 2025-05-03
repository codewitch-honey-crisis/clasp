unsigned long long adv = 0;
int tlen;
TYPE tto;
TYPE prlen;
TYPE pcmp;
int i, j;
int ch;
TYPE state = 0;
TYPE acc = -1;
int done;
bool result;
ch = (path_and_query[adv]=='\0'||path_and_query[adv]=='?') ? -1 : path_and_query[adv++];
while (ch != -1) {
	result = false;
	acc = -1;
	done = 0;
	while (!done) {
	start_dfa:
		done = 1;
		acc = fsm_data[state++];
		tlen = fsm_data[state++];
		for (i = 0; i < tlen; ++i) {
			tto = fsm_data[state++];
			prlen = fsm_data[state++];
			for (j = 0; j < prlen; ++j) {
				pcmp = fsm_data[state++];
				if (ch < pcmp) {
					state += (prlen - (j + 1));
					break;
				}
				if (ch == pcmp) {
					result = true;
					ch = (path_and_query[adv] == '\0' || path_and_query[adv] == '?') ? -1 : path_and_query[adv++];
					state = tto;
					done = 0;
					goto start_dfa;
				}
			}
		}
		if (acc != -1 && result) {
			if (path_and_query[adv]=='\0' || path_and_query[adv]=='?') {
				return (int)acc;
			}
			return -1;
		}
		ch = (path_and_query[adv] == '\0' || path_and_query[adv] == '?') ? -1 : path_and_query[adv++];
		state = 0;
	}
}
return -1;

