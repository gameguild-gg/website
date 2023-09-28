import { useRouter } from "next/router";
import { Meta } from "@/layouts/Meta";
import { Main } from "@/templates/Main";

const Index = () => {
  const router = useRouter();

  const handleLoginGoogle = async () => {
    async function signInWithGoogle() {}

    try {
      await signInWithGoogle();
      router.push("/dashboard"); // Redirecionar após o login
    } catch (error) {
      console.error("Error logging in with Google:", error);
    }
  };

  const handleLoginGitHub = async () => {
    async function signInWithGitHub() {}

    try {
      await signInWithGitHub();
      await router.push("/dashboard"); // Redirecionar após o login
    } catch (error) {
      console.error("Error logging in with GitHub:", error);
    }
  };

  return (
    <Main
      meta={
        <Meta
          title="Next.js Boilerplate Presentation"
          description="Next js Boilerplate is the perfect starter code for your project. Build your React application with the Next.js framework."
        />
      }
    >
      <div className="flex h-screen justify-center items-center">
        <div className="max-w-md w-full p-6 bg-white border rounded-lg shadow">
          <h1 className="text-2xl font-semibold mb-4">Login</h1>
          <form className="space-y-4">
            <div>
              <label htmlFor="email" className="block text-sm font-medium">
                Email
              </label>
              <input
                type="email"
                id="email"
                className="mt-1 p-2 w-full border rounded-md"
              />
            </div>
            <div>
              <label htmlFor="password" className="block text-sm font-medium">
                Password
              </label>
              <input
                type="password"
                id="password"
                className="mt-1 p-2 w-full border rounded-md"
              />
            </div>
            <button
              type="submit"
              className="w-full bg-blue-500 text-white p-2 rounded-md"
            >
              Log in
            </button>
          </form>
          <div className="mt-4">
            <button
              onClick={handleLoginGoogle}
              className="w-full bg-red-500 text-white p-2 rounded-md"
            >
              Log in with Google
            </button>
            <button
              onClick={handleLoginGitHub}
              className="w-full bg-gray-800 text-white p-2 rounded-md mt-2"
            >
              Log in with GitHub
            </button>
          </div>
        </div>
      </div>
    </Main>
  );
};

export default Index;
